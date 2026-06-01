package dev.kokonoe.wearbridge

import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.hardware.Sensor
import android.hardware.SensorEvent
import android.hardware.SensorEventListener
import android.hardware.SensorManager
import android.os.BatteryManager
import android.os.IBinder
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
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private var latestHeartRate: Double? = null
    private var latestMotion: Double? = null

    override fun onCreate() {
        super.onCreate()
        startForeground(42, notification())
        BridgeRuntimeStatus.save(this, BridgeRuntimeStatus(running = true, lastError = "starting"))
        startMotion()
        startHeartRate()
        startPostingLoop()
    }

    override fun onDestroy() {
        scope.cancel()
        val sm = getSystemService(SENSOR_SERVICE) as SensorManager
        sm.unregisterListener(this)
        BridgeRuntimeStatus.save(this, BridgeRuntimeStatus(running = false, lastError = "stopped"))
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private fun startHeartRate() {
        scope.launch {
            runCatching {
                val client = HealthServices.getClient(this@WearBridgeService).measureClient
                client.registerMeasureCallback(DataType.HEART_RATE_BPM, object : MeasureCallback {
                    override fun onAvailabilityChanged(dataType: DeltaDataType<*, *>, availability: Availability) = Unit

                    override fun onDataReceived(data: DataPointContainer) {
                        val points = data.getData(DataType.HEART_RATE_BPM)
                        latestHeartRate = points.lastOrNull()?.value
                    }
                })
            }
        }
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
                val settings = BridgeSettings.load(this@WearBridgeService)
                val sender = BridgeSender(settings)
                val battery = getSystemService(BATTERY_SERVICE) as BatteryManager
                val batteryPercent = battery.getIntProperty(BatteryManager.BATTERY_PROPERTY_CAPACITY).toDouble()
                val sample = WearableSample(
                    timestampUtc = Instant.now().toString(),
                    deviceId = settings.deviceId,
                    heartRateBpm = latestHeartRate,
                    motion = latestMotion,
                    onWrist = latestHeartRate != null,
                    activity = "watch_bridge",
                    semanticLocation = settings.semanticLocation,
                    batteryPercent = batteryPercent
                )
                val statusResult = sender.status()
                val result = if (statusResult.ok) {
                    sender.send(sample)
                } else {
                    BridgeSendResult(
                        ok = false,
                        httpCode = statusResult.httpCode,
                        error = "PC bridge status failed: ${statusResult.error.ifBlank { "unreachable" }}"
                    )
                }
                BridgeRuntimeStatus.save(
                    this@WearBridgeService,
                    BridgeRuntimeStatus(
                        running = true,
                        lastSendAt = Instant.now().toString(),
                        lastOk = result.ok,
                        lastHttpCode = result.httpCode,
                        lastError = result.error,
                        lastHeartRate = latestHeartRate?.let { "%.0f bpm".format(it) } ?: "no heart sample yet",
                        lastMotion = latestMotion?.let { "%.3f".format(it) } ?: "no motion sample yet"
                    )
                )
                delay(10_000)
            }
        }
    }

    override fun onSensorChanged(event: SensorEvent) {
        if (event.sensor.type != Sensor.TYPE_ACCELEROMETER) return
        val x = event.values.getOrNull(0) ?: 0f
        val y = event.values.getOrNull(1) ?: 0f
        val z = event.values.getOrNull(2) ?: 0f
        latestMotion = sqrt((x * x + y * y + z * z).toDouble()) / 20.0
    }

    override fun onAccuracyChanged(sensor: Sensor?, accuracy: Int) = Unit

    private fun notification(): Notification {
        val channelId = "kokonoe_wear_bridge"
        val nm = getSystemService(NOTIFICATION_SERVICE) as NotificationManager
        nm.createNotificationChannel(NotificationChannel(channelId, "Kokonoe Bridge", NotificationManager.IMPORTANCE_LOW))
        return Notification.Builder(this, channelId)
            .setContentTitle("Kokonoe Wear Bridge")
            .setContentText("Sending watch telemetry")
            .setSmallIcon(android.R.drawable.stat_sys_data_bluetooth)
            .build()
    }
}
