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
import android.text.InputType
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

    private lateinit var urlInput: EditText
    private lateinit var tokenInput: EditText
    private lateinit var locationInput: EditText
    private lateinit var autoStartSwitch: Switch
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
            setPadding(dp(16), dp(16), dp(16), dp(16))
            setBackgroundColor(Color.rgb(5, 8, 18))
        }

        root.addView(TextView(this).apply {
            text = "Kokonoe Bridge"
            textSize = 20f
            typeface = Typeface.DEFAULT_BOLD
            setTextColor(Color.rgb(245, 248, 255))
        })
        root.addView(TextView(this).apply {
            text = "watch telemetry -> desktop runtime"
            textSize = 11f
            setTextColor(Color.rgb(120, 136, 170))
        })
        root.addView(button("Setup Once") {
            setupOnce()
        }, margin(top = 12, bottom = 8).apply { height = dp(48) })

        heroStatusText = TextView(this).apply {
            textSize = 13f
            typeface = Typeface.DEFAULT_BOLD
            setPadding(dp(12), dp(10), dp(12), dp(10))
        }
        root.addView(heroStatusText, margin(top = 12, bottom = 10))

        val telemetryCard = card("Telemetry")
        val metricRow = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        bpmText = metric("Heart", "--")
        motionText = metric("Motion", "--")
        metricRow.addView(bpmText, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
        metricRow.addView(space(8))
        metricRow.addView(motionText, LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1f))
        telemetryCard.addView(metricRow)
        sendText = bodyText().apply { setPadding(0, dp(8), 0, 0) }
        telemetryCard.addView(sendText)
        root.addView(telemetryCard, margin(bottom = 10))

        val connectionCard = card("Connection")
        urlInput = edit("PC bridge URL", settings.desktopBaseUrl, InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_URI)
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
        connectionCard.addView(urlInput)
        connectionCard.addView(tokenInput)
        connectionCard.addView(locationInput)
        connectionCard.addView(autoStartSwitch)
        root.addView(connectionCard, margin(bottom = 10))

        val controlsCard = card("Controls")
        val row1 = LinearLayout(this).apply { orientation = LinearLayout.HORIZONTAL }
        row1.addView(button("Auto Find") { findPcBridge() }, LinearLayout.LayoutParams(0, dp(44), 1f))
        row1.addView(space(8))
        row1.addView(button("Test") {
            saveSettings()
            testBridge()
        }, LinearLayout.LayoutParams(0, dp(44), 1f))
        controlsCard.addView(row1)
        val row2 = LinearLayout(this).apply {
            orientation = LinearLayout.HORIZONTAL
            setPadding(0, dp(8), 0, 0)
        }
        row2.addView(button("Start") {
            saveSettings()
            startAfterPermissions = true
            if (requestPermissionsIfNeeded()) startBridgeAfterTest()
        }, LinearLayout.LayoutParams(0, dp(44), 1f))
        row2.addView(space(8))
        row2.addView(button("Stop") {
            stopService(Intent(this@MainActivity, WearBridgeService::class.java))
            refreshStatus()
        }, LinearLayout.LayoutParams(0, dp(44), 1f))
        controlsCard.addView(row2)
        root.addView(controlsCard, margin(bottom = 10))

        val diagnosticsCard = card("Diagnostics")
        errorText = bodyText().apply { setTextColor(Color.rgb(255, 170, 120)) }
        detailsText = TextView(this).apply {
            textSize = 10f
            setTextColor(Color.rgb(130, 145, 180))
            setPadding(0, dp(8), 0, 0)
        }
        diagnosticsCard.addView(errorText)
        diagnosticsCard.addView(detailsText)
        root.addView(diagnosticsCard)

        setContentView(ScrollView(this).apply { addView(root) })
    }

    private fun saveSettings() {
        val current = BridgeSettings.load(this)
        val defaults = BridgeConfig()
        BridgeSettings.save(
            this,
            BridgeSettings(
                desktopBaseUrl = urlInput.text.toString().trim().ifBlank { defaults.desktopBaseUrl },
                bridgeToken = tokenInput.text.toString().trim().ifBlank { defaults.bridgeToken },
                deviceId = current.deviceId,
                semanticLocation = locationInput.text.toString().trim().ifBlank { "unknown" },
                autoStart = autoStartSwitch.isChecked,
                pairedPcId = current.pairedPcId
            )
        )
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
            else -> "STOPPED - ready to start"
        }
        heroStatusText.text = statusText
        heroStatusText.setTextColor(if (live) Color.rgb(75, 255, 190) else Color.rgb(255, 214, 110))
        heroStatusText.background = rounded(if (live) Color.rgb(12, 42, 36) else Color.rgb(45, 34, 16), Color.rgb(55, 70, 110), 18)

        bpmText.text = "Heart\n${status.lastHeartRate.ifBlank { "--" }}"
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
                "Token: $tokenState\n" +
                "PC: ${settings.pairedPcId.ifBlank { "not paired" }}\n" +
                "Phase: ${status.phase.ifBlank { "-" }}\n" +
                "Reconnects: ${status.reconnectCount}  next: ${status.nextRetryAt.ifBlank { "-" }}\n" +
                "Location: ${settings.semanticLocation}\n" +
                "Auto start: ${if (settings.autoStart) "on" else "off"}"
    }

    private fun findPcBridge() {
        toast("Scanning local Wi-Fi")
        scope.launch {
            val settings = BridgeSettings.load(this@MainActivity)
            var result = BridgeDiscovery().find(preferredPcId = settings.pairedPcId)
            if (!result.ok && settings.pairedPcId.isNotBlank()) {
                result = BridgeDiscovery().find()
            }
            if (!result.ok) {
                toast(result.error.ifBlank { "PC bridge not found" })
                refreshStatus()
                return@launch
            }
            urlInput.setText(result.baseUrl)
            saveSettings()
            toast("Found ${result.pcName.ifBlank { result.baseUrl }}")
            testBridge()
        }
    }

    private fun setupOnce() {
        toast("Finding and pairing PC")
        scope.launch {
            if (!requestPermissionsIfNeeded()) return@launch
            val current = BridgeSettings.load(this@MainActivity)
            var found = BridgeDiscovery().find(preferredPcId = current.pairedPcId)
            if (!found.ok && current.pairedPcId.isNotBlank()) {
                found = BridgeDiscovery().find()
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

            urlInput.setText(found.baseUrl)
            autoStartSwitch.isChecked = true
            BridgeSettings.save(
                this@MainActivity,
                BridgeSettings.load(this@MainActivity).copy(
                    desktopBaseUrl = found.baseUrl,
                    pairedPcId = found.pcId,
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
        ActivityCompat.requestPermissions(this, missing.toTypedArray(), PERMISSIONS_REQUEST)
        return false
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode != PERMISSIONS_REQUEST) return
        val granted = grantResults.isNotEmpty() && grantResults.all { it == PackageManager.PERMISSION_GRANTED }
        if (!granted) {
            toast("Sensor permission denied")
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
            setPadding(dp(14), dp(12), dp(14), dp(12))
            background = rounded(Color.rgb(16, 25, 45), Color.rgb(40, 52, 92), 12)
            addView(sectionLabel(title))
        }
    }

    private fun sectionLabel(textValue: String): TextView {
        return TextView(this).apply {
            text = textValue.uppercase()
            textSize = 10f
            typeface = Typeface.DEFAULT_BOLD
            setTextColor(Color.rgb(255, 80, 120))
            setPadding(0, 0, 0, dp(8))
        }
    }

    private fun metric(label: String, value: String): TextView {
        return TextView(this).apply {
            text = "$label\n$value"
            textSize = 18f
            typeface = Typeface.DEFAULT_BOLD
            setTextColor(Color.rgb(70, 216, 255))
            setPadding(dp(10), dp(8), dp(10), dp(8))
            background = rounded(Color.rgb(9, 15, 30), Color.rgb(35, 45, 82), 10)
        }
    }

    private fun edit(hintValue: String, value: String, inputTypeValue: Int): EditText {
        return EditText(this).apply {
            hint = hintValue
            setText(value)
            setSingleLine(true)
            textSize = 12f
            inputType = inputTypeValue
            setTextColor(Color.rgb(235, 242, 255))
            setHintTextColor(Color.rgb(100, 116, 150))
            background = rounded(Color.rgb(8, 13, 28), Color.rgb(45, 57, 100), 10)
            setPadding(dp(10), 0, dp(10), 0)
            layoutParams = margin(top = 4, bottom = 6).apply { height = dp(42) }
        }
    }

    private fun button(label: String, action: () -> Unit): Button {
        return Button(this).apply {
            text = label
            textSize = 12f
            isAllCaps = false
            setTextColor(Color.rgb(235, 245, 255))
            background = rounded(Color.rgb(72, 45, 132), Color.rgb(114, 77, 210), 18)
            setOnClickListener { action() }
        }
    }

    private fun bodyText(): TextView {
        return TextView(this).apply {
            textSize = 11f
            setTextColor(Color.rgb(170, 184, 215))
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
