package dev.kokonoe.wearbridge

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Color
import android.graphics.Typeface
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.PowerManager
import android.net.Uri
import android.provider.Settings
import android.text.InputType
import android.text.TextUtils
import android.view.Gravity
import android.view.View
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.Switch
import android.widget.TextView
import android.widget.Toast
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch

class MainActivity : Activity() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private val handler = Handler(Looper.getMainLooper())
    private var startAfterPermissions = false

    private lateinit var pcIp1Input: EditText
    private lateinit var pcIp2Input: EditText
    private lateinit var pcPortInput: EditText
    private lateinit var tokenInput: EditText
    private lateinit var locationInput: EditText
    private lateinit var autoStartSwitch: Switch
    private lateinit var pairingStatusText: TextView
    private lateinit var heroStatusText: TextView
    private lateinit var bpmText: TextView
    private lateinit var motionText: TextView
    private lateinit var sendText: TextView
    private lateinit var errorText: TextView
    private lateinit var detailsText: TextView

    private val refreshRunnable = object : Runnable {
        override fun run() {
            refreshStatus()
            handler.postDelayed(this, 2_000)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        buildUi()
        requestPermissionsIfNeeded()
        refreshStatus()
        handler.post(refreshRunnable)
    }

    override fun onDestroy() {
        handler.removeCallbacks(refreshRunnable)
        scope.cancel()
        super.onDestroy()
    }

    private fun buildUi() {
        val settings = BridgeSettings.load(this)
        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(10), dp(10), dp(10), dp(16))
            setBackgroundColor(Color.rgb(5, 8, 18))
        }

        root.addView(TextView(this).apply {
            text = "Kokonoe"
            textSize = 18f
            typeface = Typeface.DEFAULT_BOLD
            setTextColor(Color.rgb(245, 248, 255))
            gravity = Gravity.CENTER
        })
        root.addView(TextView(this).apply {
            text = "WEARABLE LINK"
            textSize = 9f
            typeface = Typeface.DEFAULT_BOLD
            letterSpacing = 0.08f
            setTextColor(Color.rgb(102, 255, 220))
            gravity = Gravity.CENTER
        })
        root.addView(primaryButton("Bind Device") {
            setupOnce()
        }, margin(top = 9, bottom = 8).apply { height = dp(48) })

        heroStatusText = TextView(this).apply {
            textSize = 12f
            typeface = Typeface.DEFAULT_BOLD
            gravity = Gravity.CENTER
            setPadding(dp(10), dp(9), dp(10), dp(9))
        }
        root.addView(heroStatusText, margin(bottom = 8))

        val telemetryCard = card("Telemetry")
        val metricRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        bpmText = metric("Heart", "--")
        motionText = metric("Motion", "--")
        metricRow.addView(bpmText, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
        metricRow.addView(space(6))
        metricRow.addView(motionText, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
        telemetryCard.addView(metricRow)
        sendText = bodyText().apply { setPadding(0, dp(6), 0, 0) }
        telemetryCard.addView(sendText)
        root.addView(telemetryCard, margin(bottom = 8))

        val connectionCard = card("Bind PC")
        val urls = BridgeSettings.candidateUrls(settings)
        val port = urls.firstNotNullOfOrNull { runCatching { java.net.URI(it).port.takeIf { p -> p > 0 } }.getOrNull() }
            ?: runCatching { java.net.URI(settings.desktopBaseUrl).port.takeIf { it > 0 } }.getOrNull()
            ?: 8787
        val hosts = urls.mapNotNull { url -> runCatching { java.net.URI(url).host }.getOrNull() }
            .filter { it.isNotBlank() }
            .distinct()
        pcPortInput = edit("Port", port.toString(), InputType.TYPE_CLASS_NUMBER)
        pcIp1Input = edit("PC IP 1", hosts.getOrNull(0).orEmpty(), InputType.TYPE_CLASS_PHONE)
        pcIp2Input = edit("PC IP 2", hosts.getOrNull(1).orEmpty(), InputType.TYPE_CLASS_PHONE)
        tokenInput = edit("Bridge token", settings.bridgeToken, InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD)
        locationInput = edit("Semantic location", settings.semanticLocation, InputType.TYPE_CLASS_TEXT)
        autoStartSwitch = Switch(this).apply {
            text = "Auto start after reboot"
            textSize = 12f
            setTextColor(Color.rgb(220, 230, 255))
            isChecked = settings.autoStart
            setPadding(0, dp(6), 0, 0)
            setOnCheckedChangeListener { _, _ ->
                saveSettings()
                toast("Auto start saved")
            }
        }
        pairingStatusText = TextView(this).apply {
            text = if (settings.pairedPcId.isBlank()) "NOT BOUND YET" else "BOUND TO THIS PC: ${settings.pairedPcId.takeLast(8)}"
            textSize = 13f
            typeface = Typeface.DEFAULT_BOLD
            setTextColor(if (settings.pairedPcId.isBlank()) Color.rgb(255, 214, 110) else Color.rgb(75, 255, 190))
            gravity = Gravity.CENTER
            setPadding(0, dp(2), 0, dp(8))
        }
        connectionCard.addView(pairingStatusText)
        connectionCard.addView(pcPortInput)
        connectionCard.addView(pcIp1Input)
        connectionCard.addView(pcIp2Input)
        connectionCard.addView(tokenInput)
        connectionCard.addView(locationInput)
        connectionCard.addView(autoStartSwitch)
        root.addView(connectionCard, margin(bottom = 8))

        val controlsCard = card("Controls")
        val row1 = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        row1.addView(button("Find", Color.rgb(28, 86, 124), Color.rgb(67, 189, 255)) { findPcBridge() }, LinearLayout.LayoutParams(0, dp(44), 1f))
        row1.addView(space(6))
        row1.addView(button("Test", Color.rgb(48, 57, 92), Color.rgb(91, 110, 170)) {
            saveSettings()
            testBridge()
        }, LinearLayout.LayoutParams(0, dp(44), 1f))
        controlsCard.addView(row1)
        val row2 = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            setPadding(0, dp(6), 0, 0)
        }
        row2.addView(button("Start", Color.rgb(14, 92, 62), Color.rgb(75, 255, 190)) {
            saveSettings()
            startAfterPermissions = true
            if (requestPermissionsIfNeeded()) startBridgeAfterTest()
        }, LinearLayout.LayoutParams(0, dp(44), 1f))
        row2.addView(space(6))
        row2.addView(button("Stop", Color.rgb(98, 35, 54), Color.rgb(255, 92, 132)) {
            stopService(Intent(this@MainActivity, WearBridgeService::class.java))
            refreshStatus()
        }, LinearLayout.LayoutParams(0, dp(44), 1f))
        controlsCard.addView(row2)
        controlsCard.addView(button("Battery", Color.rgb(71, 59, 29), Color.rgb(255, 214, 110)) {
            requestBatteryOptimizationExemption()
        }, margin(top = 6).apply { height = dp(42) })
        val row3 = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            setPadding(0, dp(6), 0, 0)
        }
        row3.addView(button("Look", Color.rgb(13, 65, 84), Color.rgb(52, 211, 235)) {
            sendWatchAction("look_screen_now")
        }, LinearLayout.LayoutParams(0, dp(40), 1f))
        row3.addView(space(6))
        row3.addView(button("Note", Color.rgb(62, 55, 20), Color.rgb(255, 214, 110)) {
            sendWatchAction("note_this")
        }, LinearLayout.LayoutParams(0, dp(40), 1f))
        row3.addView(space(6))
        row3.addView(button("Stress", Color.rgb(86, 24, 48), Color.rgb(255, 92, 132)) {
            sendWatchAction("im_stressed")
        }, LinearLayout.LayoutParams(0, dp(40), 1f))
        controlsCard.addView(row3)
        root.addView(controlsCard, margin(bottom = 8))

        val diagnosticsCard = card("Diagnostics")
        errorText = bodyText().apply { setTextColor(Color.rgb(255, 170, 120)) }
        detailsText = TextView(this).apply {
            textSize = 9f
            setTextColor(Color.rgb(130, 145, 180))
            setPadding(0, dp(6), 0, 0)
            setLineSpacing(0f, 0.92f)
        }
        diagnosticsCard.addView(errorText)
        diagnosticsCard.addView(detailsText)
        root.addView(diagnosticsCard)

        setContentView(ScrollView(this).apply { addView(root) })
    }

    private fun saveSettings() {
        val current = BridgeSettings.load(this)
        val defaults = BridgeConfig()
        val port = pcPortInput.text.toString().trim().toIntOrNull()?.coerceIn(1024, 65535) ?: 8787
        val urls = pcInputUrls(port)
        val primaryUrl = urls.firstOrNull() ?: defaults.desktopBaseUrl
        val token = tokenInput.text.toString().trim()
        BridgeSettings.save(
            this,
            BridgeSettings(
                desktopBaseUrl = primaryUrl,
                bridgeToken = token,
                deviceId = current.deviceId,
                semanticLocation = locationInput.text.toString().trim().ifBlank { "unknown" },
                autoStart = autoStartSwitch.isChecked,
                pairedPcId = current.pairedPcId,
                lastSuccessfulBaseUrl = current.lastSuccessfulBaseUrl,
                knownBaseUrls = urls.joinToString("\n")
            )
        )
    }

    private fun sendWatchAction(action: String) {
        saveSettings()
        scope.launch {
            val result = BridgeSender(BridgeSettings.load(this@MainActivity)).sendAction(action)
            if (result.ok) {
                toast("Sent: $action")
                BridgeLog.append(this@MainActivity, "watch action sent: $action")
            } else {
                val error = result.error.ifBlank { "HTTP ${result.httpCode}" }
                toast("Action failed: $error")
                BridgeLog.append(this@MainActivity, "watch action failed: $action $error")
            }
            refreshStatus()
        }
    }

    private fun pcInputUrls(port: Int): List<String> {
        return listOf(pcIp1Input.text.toString(), pcIp2Input.text.toString())
            .map { normalizePcInput(it, port) }
            .filter { it.isNotBlank() }
            .distinct()
    }

    private fun normalizePcInput(value: String, port: Int): String {
        var text = value.trim().trimEnd('/')
        if (text.isBlank()) return ""
        if (!text.startsWith("http://") && !text.startsWith("https://")) {
            text = "http://$text"
        }
        val uri = runCatching { java.net.URI(text) }.getOrNull() ?: return ""
        val host = uri.host ?: return ""
        val actualPort = if (uri.port > 0) uri.port else port
        return "${uri.scheme}://$host:$actualPort"
    }

    private fun applyUrlToIpInputs(url: String) {
        val uri = runCatching { java.net.URI(url) }.getOrNull() ?: return
        val host = uri.host ?: return
        val port = if (uri.port > 0) uri.port else pcPortInput.text.toString().toIntOrNull() ?: 8787
        pcPortInput.setText(port.toString())
        if (pcIp1Input.text.isNullOrBlank() || pcIp1Input.text.toString() == host) {
            pcIp1Input.setText(host)
        } else if (pcIp2Input.text.isNullOrBlank() || pcIp2Input.text.toString() == host) {
            pcIp2Input.setText(host)
        }
    }

    private fun refreshStatus() {
        val settings = BridgeSettings.load(this)
        val status = BridgeRuntimeStatus.load(this)
        val hasError = status.lastError.isNotBlank()
        val live = status.running && status.lastOk && !hasError

        val statusText = when {
            live -> "LIVE - desktop accepts samples"
            status.running && hasError -> "RUNNING - attention required"
            status.running -> "RUNNING - waiting for first sample"
            settings.pairedPcId.isNotBlank() -> "BOUND - press Start"
            else -> "STOPPED - ready to start"
        }
        heroStatusText.text = statusText
        heroStatusText.setTextColor(if (live) Color.rgb(75, 255, 190) else Color.rgb(255, 214, 110))
        heroStatusText.background = rounded(if (live) Color.rgb(12, 42, 36) else Color.rgb(45, 34, 16), Color.rgb(55, 70, 110), 18)
        pairingStatusText.text = if (settings.pairedPcId.isBlank()) "NOT BOUND YET" else "BOUND TO THIS PC: ${settings.pairedPcId.takeLast(8)}"
        pairingStatusText.setTextColor(if (settings.pairedPcId.isBlank()) Color.rgb(255, 214, 110) else Color.rgb(75, 255, 190))

        bpmText.text = "Heart\n${status.lastHeartRate.ifBlank { "--" }}\n${status.heartStatus.ifBlank { "not started" }}"
        motionText.text = "Motion\n${if (status.lastMotion.isNotBlank()) status.lastMotion else "--"}"
        sendText.text = "Last send: ${if (status.lastSendAt.isNotBlank()) status.lastSendAt else "never"}  |  HTTP ${if (status.lastHttpCode > 0) status.lastHttpCode else "--"}"
        errorText.text = if (hasError) status.lastError else "No current bridge error."

        val defaults = BridgeConfig()
        val tokenState = when {
            settings.bridgeToken == defaults.bridgeToken && settings.bridgeToken.contains("PASTE") -> "placeholder"
            settings.bridgeToken.length >= 6 -> "set ...${settings.bridgeToken.takeLast(4)}"
            else -> "missing"
        }
        detailsText.text =
                "URL: ${settings.desktopBaseUrl}\n" +
                "Known URLs: ${BridgeSettings.candidateUrls(settings).joinToString(", ").ifBlank { "-" }}\n" +
                "Token: $tokenState\n" +
                "PC: ${settings.pairedPcId.ifBlank { "not paired" }}\n" +
                "Heart sensor: ${status.heartStatus.ifBlank { "not started" }}\n" +
                "Phase: ${status.phase.ifBlank { "-" }}\n" +
                "Active URL: ${status.activeBaseUrl.ifBlank { settings.desktopBaseUrl }}\n" +
                "Known URL count: ${status.knownUrlCount}\n" +
                "Reconnects: ${status.reconnectCount}  attempts: ${status.lastAttempts}  next: ${status.nextRetryAt.ifBlank { "-" }}\n" +
                "Queued: ${status.queuedSamples}\n" +
                "Location: ${settings.semanticLocation}\n" +
                "Auto start: ${if (settings.autoStart) "on" else "off"}\n" +
                "Log:\n${status.logTail.ifBlank { "-" }}"
    }

    private fun findPcBridge() {
        toast("Scanning local Wi-Fi")
        scope.launch {
            val settings = BridgeSettings.load(this@MainActivity)
            var result = BridgeDiscovery(this@MainActivity).find(
                preferredPcId = settings.pairedPcId,
                preferredBaseUrl = settings.lastSuccessfulBaseUrl,
                candidateBaseUrls = BridgeSettings.candidateUrls(settings),
                scanSubnet = true
            )
            if (!result.ok && settings.pairedPcId.isNotBlank()) {
                result = BridgeDiscovery(this@MainActivity).find()
            }
            if (!result.ok) {
                toast(result.error.ifBlank { "PC bridge not found" })
                refreshStatus()
                return@launch
            }
            applyUrlToIpInputs(result.baseUrl)
            saveSettings()
            toast("Found ${result.pcName.ifBlank { result.baseUrl }}")
            testBridge()
        }
    }

    private fun setupOnce() {
        toast("Finding and pairing PC")
        scope.launch {
            if (!requestPermissionsIfNeeded()) return@launch
            saveSettings()
            val directPair = BridgeAutoConnector.pairCurrentUrl(this@MainActivity)
            if (directPair.ok) {
                tokenInput.setText(directPair.token)
                pairingStatusText.text = "BOUND TO THIS PC: ${directPair.pcId.takeLast(8)}"
                pairingStatusText.setTextColor(Color.rgb(75, 255, 190))
                BridgeRuntimeStatus.save(
                    this@MainActivity,
                    BridgeRuntimeStatus.load(this@MainActivity).copy(
                        lastOk = true,
                        lastHttpCode = directPair.httpCode,
                        lastError = "",
                        phase = "paired_direct",
                        pairedPcId = directPair.pcId,
                        pcName = directPair.pcName,
                        activeBaseUrl = BridgeSettings.load(this@MainActivity).desktopBaseUrl,
                        knownUrlCount = BridgeSettings.candidateUrls(BridgeSettings.load(this@MainActivity)).size
                    )
                )
                startBridgeAfterTest()
                toast("Paired ${directPair.pcName.ifBlank { directPair.pcId }}")
                refreshStatus()
                return@launch
            }

            val current = BridgeSettings.load(this@MainActivity)
            var found = BridgeDiscovery(this@MainActivity).find(
                preferredPcId = current.pairedPcId,
                preferredBaseUrl = current.lastSuccessfulBaseUrl,
                candidateBaseUrls = BridgeSettings.candidateUrls(current),
                scanSubnet = true
            )
            if (!found.ok && current.pairedPcId.isNotBlank()) {
                found = BridgeDiscovery(this@MainActivity).find()
            }
            if (!found.ok) {
                BridgeRuntimeStatus.save(
                    this@MainActivity,
                    BridgeRuntimeStatus.load(this@MainActivity).copy(lastOk = false, lastError = found.error)
                )
                toast(found.error.ifBlank { "PC not found" })
                refreshStatus()
                return@launch
            }

            applyUrlToIpInputs(found.baseUrl)
            autoStartSwitch.isChecked = true
            BridgeSettings.save(
                this@MainActivity,
                BridgeSettings.load(this@MainActivity).copy(
                    desktopBaseUrl = found.baseUrl,
                    pairedPcId = found.pcId,
                    lastSuccessfulBaseUrl = found.baseUrl,
                    knownBaseUrls = BridgeSettings.mergeKnownUrls(BridgeSettings.load(this@MainActivity), found.baseUrl),
                    autoStart = true,
                    semanticLocation = locationInput.text.toString().trim().ifBlank { "unknown" }
                )
            )

            val pair = BridgeAutoConnector.pairCurrentUrl(this@MainActivity)
            if (!pair.ok) {
                BridgeRuntimeStatus.save(
                    this@MainActivity,
                    BridgeRuntimeStatus.load(this@MainActivity).copy(lastOk = false, lastHttpCode = pair.httpCode, lastError = pair.error)
                )
                toast("Pair failed: ${pair.error.ifBlank { pair.httpCode.toString() }}")
                refreshStatus()
                return@launch
            }

            tokenInput.setText(pair.token)
            BridgeRuntimeStatus.save(
                this@MainActivity,
                BridgeRuntimeStatus.load(this@MainActivity).copy(
                    lastOk = true,
                    lastHttpCode = pair.httpCode,
                    lastError = "",
                    phase = "paired",
                    pairedPcId = pair.pcId,
                    pcName = pair.pcName,
                    reconnectCount = 0,
                    nextRetryAt = ""
                )
            )
            pairingStatusText.text = "BOUND TO THIS PC: ${pair.pcId.takeLast(8)}"
            pairingStatusText.setTextColor(Color.rgb(75, 255, 190))
            startBridgeAfterTest()
            toast("Paired ${pair.pcName.ifBlank { pair.pcId }}")
            refreshStatus()
        }
    }

    private fun testBridge() {
        toast("Testing bridge")
        scope.launch {
            val result = BridgeSender(BridgeSettings.load(this@MainActivity)).status()
            BridgeRuntimeStatus.save(
                this@MainActivity,
                BridgeRuntimeStatus.load(this@MainActivity).copy(
                    lastOk = result.ok,
                    lastHttpCode = result.httpCode,
                    lastError = result.error ?: "",
                    phase = if (result.ok) "pc_reachable" else "pc_unreachable",
                    pairedPcId = BridgeSettings.load(this@MainActivity).pairedPcId,
                    pcName = result.pcName
                )
            )
            toast(if (result.ok) "Desktop bridge OK" else "Bridge failed: ${result.error ?: result.httpCode}")
            refreshStatus()
        }
    }

    private fun startBridgeAfterTest() {
        scope.launch {
            val result = BridgeSender(BridgeSettings.load(this@MainActivity)).status()
            BridgeRuntimeStatus.save(
                this@MainActivity,
                BridgeRuntimeStatus.load(this@MainActivity).copy(
                    lastOk = result.ok,
                    lastHttpCode = result.httpCode,
                    lastError = result.error ?: "",
                    phase = if (result.ok) "pc_reachable" else "pc_unreachable",
                    pairedPcId = BridgeSettings.load(this@MainActivity).pairedPcId,
                    pcName = result.pcName
                )
            )
            if (!result.ok) {
                toast("Fix bridge first: ${result.error ?: result.httpCode}")
                refreshStatus()
                return@launch
            }
            startBridge()
        }
    }

    private fun startBridge() {
        ContextCompat.startForegroundService(this, Intent(this, WearBridgeService::class.java))
        BridgeRuntimeStatus.save(this, BridgeRuntimeStatus.load(this).copy(running = true, lastError = "", phase = "starting"))
        toast("Bridge started")
        refreshStatus()
    }

    private fun requestPermissionsIfNeeded(): Boolean {
        val permissions = mutableListOf(
            Manifest.permission.BODY_SENSORS,
            Manifest.permission.ACTIVITY_RECOGNITION
        )
        if (Build.VERSION.SDK_INT >= 33) {
            permissions.add(Manifest.permission.POST_NOTIFICATIONS)
        }
        val missing = permissions.filter {
            ContextCompat.checkSelfPermission(this, it) != PackageManager.PERMISSION_GRANTED
        }
        if (missing.isEmpty()) return true
        toast("Permission Required")
        ActivityCompat.requestPermissions(this, missing.toTypedArray(), PERMISSIONS_REQUEST)
        return false
    }

    private fun requestBatteryOptimizationExemption() {
        val pm = getSystemService(POWER_SERVICE) as PowerManager
        if (pm.isIgnoringBatteryOptimizations(packageName)) {
            toast("Battery optimization already disabled")
            return
        }
        runCatching {
            startActivity(
                Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS).apply {
                    data = Uri.parse("package:$packageName")
                }
            )
        }.onFailure {
            startActivity(Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS))
        }
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode != PERMISSIONS_REQUEST) return
        val granted = grantResults.isNotEmpty() && grantResults.all { it == PackageManager.PERMISSION_GRANTED }
        if (!granted) {
            toast("Permission Required")
            startAfterPermissions = false
            return
        }
        if (startAfterPermissions) {
            startAfterPermissions = false
            startBridgeAfterTest()
        }
    }

    private fun card(title: String): LinearLayout {
        return LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(dp(10), dp(9), dp(10), dp(10))
            background = rounded(Color.rgb(13, 22, 42), Color.rgb(44, 58, 102), 10)
            addView(sectionLabel(title))
        }
    }

    private fun sectionLabel(textValue: String): TextView {
        return TextView(this).apply {
            text = textValue.uppercase()
            textSize = 9f
            typeface = Typeface.DEFAULT_BOLD
            setTextColor(Color.rgb(255, 80, 120))
            gravity = Gravity.CENTER
            letterSpacing = 0.08f
            setPadding(0, 0, 0, dp(7))
        }
    }

    private fun metric(label: String, value: String): TextView {
        return TextView(this).apply {
            text = "$label\n$value"
            textSize = 13f
            typeface = Typeface.DEFAULT_BOLD
            gravity = Gravity.CENTER
            setTextColor(Color.rgb(70, 216, 255))
            setPadding(dp(8), dp(7), dp(8), dp(7))
            minHeight = dp(64)
            background = rounded(Color.rgb(7, 13, 28), Color.rgb(35, 54, 96), 9)
        }
    }

    private fun edit(hintValue: String, value: String, inputTypeValue: Int): EditText {
        return EditText(this).apply {
            hint = hintValue
            setText(value)
            setSingleLine(true)
            textSize = 12f
            gravity = Gravity.CENTER
            inputType = inputTypeValue
            ellipsize = TextUtils.TruncateAt.END
            setTextColor(Color.rgb(235, 242, 255))
            setHintTextColor(Color.rgb(100, 116, 150))
            background = rounded(Color.rgb(7, 12, 26), Color.rgb(46, 60, 105), 9)
            setPadding(dp(9), 0, dp(9), 0)
            layoutParams = margin(top = 3, bottom = 5).apply { height = dp(40) }
        }
    }

    private fun primaryButton(label: String, action: () -> Unit): Button {
        return button(label, action).apply {
            textSize = 14f
            typeface = Typeface.DEFAULT_BOLD
            background = rounded(Color.rgb(232, 51, 97), Color.rgb(255, 122, 158), 22)
        }
    }

    private fun button(label: String, action: () -> Unit): Button {
        return button(label, Color.rgb(64, 45, 118), Color.rgb(103, 78, 190), action)
    }

    private fun button(label: String, fill: Int, stroke: Int, action: () -> Unit): Button {
        return Button(this).apply {
            text = label
            textSize = 12f
            typeface = Typeface.DEFAULT_BOLD
            isAllCaps = false
            gravity = Gravity.CENTER
            setTextColor(Color.rgb(235, 245, 255))
            background = rounded(fill, stroke, 18)
            minHeight = 0
            minimumHeight = 0
            setPadding(dp(6), 0, dp(6), 0)
            setOnClickListener { action() }
        }
    }

    private fun bodyText(): TextView {
        return TextView(this).apply {
            textSize = 11f
            setTextColor(Color.rgb(170, 184, 215))
            gravity = Gravity.CENTER
            setLineSpacing(0f, 0.94f)
        }
    }

    private fun space(widthDp: Int): View {
        return View(this).apply {
            layoutParams = LinearLayout.LayoutParams(dp(widthDp), 1)
        }
    }

    private fun margin(top: Int = 0, bottom: Int = 0): LinearLayout.LayoutParams {
        return LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT).apply {
            setMargins(0, dp(top), 0, dp(bottom))
        }
    }

    private fun rounded(fill: Int, stroke: Int, radiusDp: Int): GradientDrawable {
        return GradientDrawable().apply {
            setColor(fill)
            cornerRadius = dp(radiusDp).toFloat()
            setStroke(dp(1), stroke)
        }
    }

    private fun dp(value: Int): Int = (value * resources.displayMetrics.density).toInt()

    private fun toast(message: String) {
        Toast.makeText(this, message, Toast.LENGTH_SHORT).show()
    }

    companion object {
        private const val PERMISSIONS_REQUEST = 43
    }
}
