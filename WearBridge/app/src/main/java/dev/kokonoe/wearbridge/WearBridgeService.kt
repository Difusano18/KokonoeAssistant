package dev.kokonoe.wearbridge

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.os.BatteryManager
import android.os.IBinder
import android.os.PowerManager
import android.util.Log
import androidx.health.services.client.HealthServices
import androidx.health.services.client.MeasureCallback
import androidx.health.services.client.data.DataPointContainer
import androidx.health.services.client.data.DataType
import androidx.health.services.client.data.DeltaDataType
import androidx.health.services.client.data.Availability
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import java.time.Instant
import kotlin.math.sqrt

class WearBridgeService : Service(), SensorEventListener {
    private val tag = "KokonoeWearBridge"
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private var latestHeartRate: Double? = null
    private var latestHeartSource: String = ""
    private var latestMotion: Double? = null
    private var reconnectCount = 0
    private var nextDiscoveryAtMs = 0L
    private var wakeLock: PowerManager.WakeLock? = null
    private lateinit var sampleStore: BridgeSampleStore

    override fun onCreate() {
        super.onCreate()
        sampleStore = BridgeSampleStore(this)
        startForeground(42, notification("Starting", "Preparing sensors and bridge"))
        val settings = BridgeSettings.load(this)
        Log.d(tag, "service starting")
        BridgeLog.append(this, "service starting")
        BridgeRuntimeStatus.save(
            this,
            BridgeRuntimeStatus(
                running = true,
                lastError = "starting",
                phase = if (settings.pairedPcId.isBlank()) "needs_pairing" else "starting",
                pairedPcId = settings.pairedPcId,
                heartStatus = "heart sensor starting",
                queuedSamples = sampleStore.count(),
                activeBaseUrl = settings.desktopBaseUrl,
                knownUrlCount = BridgeSettings.candidateUrls(settings).size,
                logTail = BridgeLog.tail(this)
            )
        )
        startMotion()
        startHeartRate()
        startPostingLoop()
    }

    override fun onDestroy() {
        scope.cancel()
        releaseWakeLock()
        val sm = getSystemService(SENSOR_SERVICE) as SensorManager
        sm.unregisterListener(this)
        Log.d(tag, "service stopped")
        BridgeLog.append(this, "service stopped")
        BridgeRuntimeStatus.save(this, BridgeRuntimeStatus(running = false, lastError = "stopped"))
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private fun startHeartRate() {
        if (checkSelfPermission(Manifest.permission.BODY_SENSORS) != PackageManager.PERMISSION_GRANTED) {
            BridgeLog.append(this, "heart permission missing")
            BridgeRuntimeStatus.save(
                this,
                BridgeRuntimeStatus.load(this).copy(heartStatus = "BODY_SENSORS permission missing")
            )
            return
        }
        scope.launch {
            runCatching {
                val client = HealthServices.getClient(this@WearBridgeService).measureClient
                client.registerMeasureCallback(DataType.HEART_RATE_BPM, object : MeasureCallback {
                    override fun onAvailabilityChanged(dataType: DeltaDataType<*, *>, availability: Availability) {
                        BridgeRuntimeStatus.save(
                            this@WearBridgeService,
                            BridgeRuntimeStatus.load(this@WearBridgeService).copy(heartStatus = "heart availability: $availability")
                        )
                    }

                    override fun onDataReceived(data: DataPointContainer) {
                        val points = data.getData(DataType.HEART_RATE_BPM)
                        val bpm = points.lastOrNull()?.value
                        if (bpm != null) {
                            updateHeartRate(bpm, "health_services")
                        } else {
                            BridgeRuntimeStatus.save(
                                this@WearBridgeService,
                                BridgeRuntimeStatus.load(this@WearBridgeService).copy(heartStatus = "health services callback without bpm; waiting for sensor")
                            )
                        }
                    }
                })
                BridgeRuntimeStatus.save(
                    this@WearBridgeService,
                    BridgeRuntimeStatus.load(this@WearBridgeService).copy(heartStatus = "heart measure registered")
                )
                startHeartRateSensorFallback()
            }.onFailure {
                BridgeLog.append(this@WearBridgeService, "heart sensor failed: ${it.message}")
                BridgeRuntimeStatus.save(
                    this@WearBridgeService,
                    BridgeRuntimeStatus.load(this@WearBridgeService).copy(heartStatus = "heart sensor failed: ${it.message ?: it.javaClass.simpleName}")
                )
                startHeartRateSensorFallback()
            }
        }
    }

    private fun startHeartRateSensorFallback() {
        runCatching {
            val sm = getSystemService(SENSOR_SERVICE) as SensorManager
            val heart = sm.getDefaultSensor(Sensor.TYPE_HEART_RATE)
            if (heart == null) {
                BridgeLog.append(this, "direct heart sensor unavailable")
                BridgeRuntimeStatus.save(
                    this,
                    BridgeRuntimeStatus.load(this).copy(heartStatus = "health services registered; direct heart sensor unavailable")
                )
                return
            }

            val ok = sm.registerListener(this, heart, SensorManager.SENSOR_DELAY_NORMAL)
            BridgeLog.append(this, if (ok) "direct heart sensor registered: ${heart.name}" else "direct heart sensor registration failed")
            BridgeRuntimeStatus.save(
                this,
                BridgeRuntimeStatus.load(this).copy(
                    heartStatus = if (ok) "heart measure registered + direct sensor ${heart.name}" else "heart measure registered; direct sensor registration failed"
                )
            )
        }.onFailure {
            BridgeLog.append(this, "direct heart sensor failed: ${it.message}")
            BridgeRuntimeStatus.save(
                this,
                BridgeRuntimeStatus.load(this).copy(heartStatus = "direct heart sensor failed: ${it.message ?: it.javaClass.simpleName}")
            )
        }
    }

    private fun updateHeartRate(bpm: Double, source: String) {
        if (bpm < 25.0 || bpm > 230.0) return
        latestHeartRate = bpm
        latestHeartSource = source
        BridgeRuntimeStatus.save(
            this,
            BridgeRuntimeStatus.load(this).copy(
                lastHeartRate = "%.0f bpm".format(bpm),
                heartStatus = "heart sample %.0f bpm via %s".format(bpm, source)
            )
        )
    }

    private fun startMotion() {
        runCatching {
            val sm = getSystemService(SENSOR_SERVICE) as SensorManager
            val accelerometer = sm.getDefaultSensor(Sensor.TYPE_ACCELEROMETER) ?: return
            sm.registerListener(this, accelerometer, SensorManager.SENSOR_DELAY_NORMAL)
        }
    }

    private fun startPostingLoop() {
        scope.launch {
            while (true) {
                acquireWakeLock()
                try {
                    var settings = BridgeSettings.load(this@WearBridgeService)
                    var sender = BridgeSender(settings)
                    val battery = getSystemService(BATTERY_SERVICE) as BatteryManager
                    val batteryPercent = battery.getIntProperty(BatteryManager.BATTERY_PROPERTY_CAPACITY).toDouble()
                    val now = Instant.now()
                    val sample = WearableSample(
                        sampleId = "${settings.deviceId}-${now.toEpochMilli()}",
                        timestampUtc = now.toString(),
                        deviceId = settings.deviceId,
                        heartRateBpm = latestHeartRate,
                        motion = latestMotion,
                        onWrist = latestHeartRate != null,
                        activity = "watch_bridge",
                        semanticLocation = settings.semanticLocation,
                        batteryPercent = batteryPercent
                    )
                    var phase = "checking_pc"

                    if (settings.pairedPcId.isBlank() || settings.bridgeToken == BridgeConfig().bridgeToken) {
                        phase = "auto_pairing"
                        val pair = BridgeAutoConnector.discoverAndPair(this@WearBridgeService)
                        if (pair.ok) {
                            BridgeLog.append(this@WearBridgeService, "auto paired ${pair.pcName.ifBlank { pair.pcId }}")
                            settings = BridgeSettings.load(this@WearBridgeService)
                            sender = BridgeSender(settings)
                            reconnectCount = 0
                        } else {
                            BridgeLog.append(this@WearBridgeService, "auto pair failed: ${pair.error}")
                        }
                    }

                    var statusResult = sender.status()
                    if (statusResult.ok) {
                        BridgeSettings.save(
                            this@WearBridgeService,
                            settings.copy(
                                lastSuccessfulBaseUrl = settings.desktopBaseUrl,
                                pairedPcId = statusResult.pcId.ifBlank { settings.pairedPcId },
                                knownBaseUrls = BridgeSettings.mergeKnownUrls(
                                    settings.copy(knownBaseUrls = "${settings.knownBaseUrls}\n${statusResult.knownBaseUrls}"),
                                    settings.desktopBaseUrl
                                )
                            )
                        )
                        reconnectCount = 0
                        nextDiscoveryAtMs = 0L
                    } else if (settings.pairedPcId.isNotBlank()) {
                        val nowMs = System.currentTimeMillis()
                        if (nowMs >= nextDiscoveryAtMs) {
                            phase = "reconnecting"
                            reconnectCount += 1
                            val reconnect = BridgeAutoConnector.reconnectKnownPc(this@WearBridgeService)
                            if (reconnect.ok) {
                                settings = BridgeSettings.load(this@WearBridgeService)
                                sender = BridgeSender(settings)
                                statusResult = sender.status()
                                phase = "reconnected"
                                nextDiscoveryAtMs = 0L
                                BridgeLog.append(this@WearBridgeService, "reconnected ${reconnect.baseUrl}")
                            } else {
                                val backoffMs = reconnectBackoffMs(reconnectCount)
                                nextDiscoveryAtMs = nowMs + backoffMs
                                phase = "waiting_retry"
                                BridgeLog.append(this@WearBridgeService, "reconnect failed: ${reconnect.error}")
                            }
                        } else {
                            phase = "waiting_retry"
                        }
                    }

                    val result = if (statusResult.ok) {
                        phase = "sending"
                        flushQueued(sender)
                        val sent = sender.send(sample)
                        if (!sent.ok) {
                            sampleStore.enqueue(sample)
                            BridgeLog.append(this@WearBridgeService, "send failed; queued sample: ${sent.error.ifBlank { sent.httpCode.toString() }}")
                        } else {
                            handleCommand(sender.nextCommand())
                        }
                        sent
                    } else {
                        sampleStore.enqueue(sample)
                        BridgeSendResult(
                            ok = false,
                            httpCode = statusResult.httpCode,
                            error = "PC bridge status failed: ${statusResult.error.ifBlank { "unreachable" }}"
                        )
                    }

                    val runtime = BridgeRuntimeStatus(
                            running = true,
                            lastSendAt = Instant.now().toString(),
                            lastOk = result.ok,
                            lastHttpCode = result.httpCode,
                            lastError = result.error,
                            lastHeartRate = latestHeartRate?.let { "%.0f bpm".format(it) } ?: "no heart sample yet",
                            heartStatus = BridgeRuntimeStatus.load(this@WearBridgeService).heartStatus.ifBlank {
                                if (latestHeartRate == null) "waiting for heart sample" else "heart sample received"
                            },
                            lastMotion = latestMotion?.let { "%.3f".format(it) } ?: "no motion sample yet",
                            phase = if (result.ok) "live" else phase,
                            pairedPcId = settings.pairedPcId,
                            pcName = statusResult.pcName,
                            reconnectCount = reconnectCount,
                            nextRetryAt = if (nextDiscoveryAtMs > 0) Instant.ofEpochMilli(nextDiscoveryAtMs).toString() else "",
                            queuedSamples = sampleStore.count(),
                            activeBaseUrl = settings.desktopBaseUrl,
                            knownUrlCount = BridgeSettings.candidateUrls(settings).size,
                            lastAttempts = result.attempts,
                            logTail = BridgeLog.tail(this@WearBridgeService)
                        )
                    BridgeRuntimeStatus.save(this@WearBridgeService, runtime)
                    updateNotification(runtime)
                } catch (ex: Throwable) {
                    Log.e(tag, "posting loop iteration failed", ex)
                    BridgeLog.append(this@WearBridgeService, "loop failed: ${ex.javaClass.simpleName}: ${ex.message}")
                    val runtime = BridgeRuntimeStatus.load(this@WearBridgeService).copy(
                        running = true,
                        lastOk = false,
                        lastError = "${ex.javaClass.simpleName}: ${ex.message ?: "loop failed"}",
                        phase = "loop_error",
                        logTail = BridgeLog.tail(this@WearBridgeService)
                    )
                    BridgeRuntimeStatus.save(this@WearBridgeService, runtime)
                    updateNotification(runtime)
                } finally {
                    releaseWakeLock()
                    delay(10_000)
                }
            }
        }
    }

    override fun onSensorChanged(event: SensorEvent) {
        when (event.sensor.type) {
            Sensor.TYPE_ACCELEROMETER -> {
                val x = event.values.getOrNull(0) ?: 0f
                val y = event.values.getOrNull(1) ?: 0f
                val z = event.values.getOrNull(2) ?: 0f
                latestMotion = sqrt((x * x + y * y + z * z).toDouble()) / 20.0
            }
            Sensor.TYPE_HEART_RATE -> {
                val bpm = event.values.getOrNull(0)?.toDouble() ?: return
                updateHeartRate(bpm, "sensor_manager")
            }
        }
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) = Unit

    private fun reconnectBackoffMs(count: Int): Long {
        val base = when {
            count <= 0 -> 15_000L
            count == 1 -> 30_000L
            count == 2 -> 60_000L
            count == 3 -> 120_000L
            else -> 240_000L
        }
        return base + kotlin.random.Random.nextLong(0, 5_000L)
    }

    private suspend fun flushQueued(sender: BridgeSender) {
        val batch = sampleStore.peekBatch()
        if (batch.isEmpty()) return

        val batchResult = sender.sendJsonBatch(batch)
        if (batchResult.ok) {
            sampleStore.removeSent(batch.size)
            BridgeLog.append(this, "flushed ${batch.size} queued samples as batch")
            return
        }

        var fallbackSent = 0
        for (json in batch.take(6)) {
            val result = sender.sendJson(json)
            if (!result.ok) break
            fallbackSent += 1
        }
        if (fallbackSent > 0) {
            sampleStore.removeSent(fallbackSent)
            BridgeLog.append(this, "flushed $fallbackSent queued samples individually after batch failed")
        } else {
            BridgeLog.append(this, "batch flush failed: ${batchResult.error.ifBlank { batchResult.httpCode.toString() }}")
        }
    }

    private suspend fun handleCommand(result: BridgeSendResult) {
        if (!result.ok || result.commandAction.isBlank()) return
        BridgeLog.append(this, "command ${result.commandAction} ${result.commandId}")
        var ackOk = true
        var ackDetail = "handled"
        when (result.commandAction.lowercase()) {
            "refresh_pairing" -> {
                val pair = BridgeAutoConnector.discoverAndPair(this)
                ackOk = pair.ok
                ackDetail = if (pair.ok) "pairing refreshed" else pair.error.ifBlank { "pairing failed" }
                BridgeLog.append(this, if (pair.ok) "command pairing refreshed" else "command pairing failed: ${pair.error}")
            }
            "clear_queue" -> {
                sampleStore.clear()
                ackDetail = "queued samples cleared"
                BridgeLog.append(this, "queued samples cleared")
            }
            "restart_service" -> {
                BridgeLog.append(this, "restart requested; restarting loop state")
                reconnectCount = 0
                nextDiscoveryAtMs = 0L
                ackDetail = "loop state reset"
            }
            else -> {
                ackOk = false
                ackDetail = "unknown command"
                BridgeLog.append(this, "unknown command ${result.commandAction}")
            }
        }
        val settings = BridgeSettings.load(this)
        val ack = BridgeSender(settings).ackCommand(result.commandId, result.commandAction, ackOk, ackDetail)
        if (!ack.ok) {
            BridgeLog.append(this, "command ack failed: ${ack.error.ifBlank { ack.httpCode.toString() }}")
        }
    }

    private fun acquireWakeLock() {
        runCatching {
            val pm = getSystemService(POWER_SERVICE) as PowerManager
            val lock = wakeLock ?: pm.newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "KokonoeBridge:TelemetryBurst").also {
                it.setReferenceCounted(false)
                wakeLock = it
            }
            if (!lock.isHeld) lock.acquire(20_000L)
        }.onFailure {
            Log.e(tag, "wake lock acquire failed", it)
        }
    }

    private fun releaseWakeLock() {
        runCatching {
            wakeLock?.takeIf { it.isHeld }?.release()
        }.onFailure {
            Log.e(tag, "wake lock release failed", it)
        }
    }

    private fun updateNotification(status: BridgeRuntimeStatus) {
        val nm = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        val title = when {
            status.lastOk -> "Connected"
            status.phase.contains("reconnect", ignoreCase = true) || status.phase.contains("pair", ignoreCase = true) -> "Scanning"
            else -> "Disconnected"
        }
        val detail = "${status.phase.ifBlank { "waiting" }} / ${status.activeBaseUrl.ifBlank { "no PC" }}"
        nm.notify(42, notification(title, detail))
    }

    private fun notification(status: String, detail: String): Notification {
        val channelId = "kokonoe_wear_bridge"
        val nm = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        nm.createNotificationChannel(NotificationChannel(channelId, "Kokonoe Bridge", NotificationManager.IMPORTANCE_LOW))
        val intent = Intent(this, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            this,
            42,
            intent,
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        return Notification.Builder(this, channelId)
            .setContentTitle("Kokonoe Wear Bridge")
            .setContentText("$status - $detail")
            .setSmallIcon(android.R.drawable.stat_sys_data_bluetooth)
            .setOngoing(true)
            .setContentIntent(pendingIntent)
            .build()
    }
}
