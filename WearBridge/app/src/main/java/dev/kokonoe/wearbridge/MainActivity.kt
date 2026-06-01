package dev.kokonoe.wearbridge

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.text.InputType
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.ScrollView
import android.widget.TextView
import android.widget.Toast
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class MainActivity : Activity() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private val handler = Handler(Looper.getMainLooper())
    private var startAfterPermissions = false
    private lateinit var urlInput: EditText
    private lateinit var tokenInput: EditText
    private lateinit var locationInput: EditText
    private lateinit var statusText: TextView

    private val refreshRunnable = object : Runnable {
        override fun run() {
            refreshStatus()
            handler.postDelayed(this, 2000)
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
        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(18, 18, 18, 18)
        }

        layout.addView(TextView(this).apply {
            text = "Kokonoe Wear Bridge"
            textSize = 18f
        })

        urlInput = edit("PC bridge URL", settings.desktopBaseUrl, InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_URI)
        tokenInput = edit("Bridge token", settings.bridgeToken, InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_VISIBLE_PASSWORD)
        locationInput = edit("Semantic location", settings.semanticLocation, InputType.TYPE_CLASS_TEXT)

        layout.addView(urlInput)
        layout.addView(tokenInput)
        layout.addView(locationInput)

        layout.addView(Button(this).apply {
            text = "Save"
            setOnClickListener {
                saveSettings()
                toast("Saved")
            }
        })

        layout.addView(Button(this).apply {
            text = "Auto Find PC"
            setOnClickListener { findPcBridge() }
        })

        layout.addView(Button(this).apply {
            text = "Test PC"
            setOnClickListener {
                saveSettings()
                testBridge()
            }
        })

        layout.addView(Button(this).apply {
            text = "Start"
            setOnClickListener {
                saveSettings()
                startAfterPermissions = true
                if (requestPermissionsIfNeeded()) startBridgeAfterTest()
            }
        })

        layout.addView(Button(this).apply {
            text = "Stop"
            setOnClickListener {
                stopService(Intent(this@MainActivity, WearBridgeService::class.java))
                refreshStatus()
            }
        })

        statusText = TextView(this).apply {
            textSize = 12f
            setPadding(0, 12, 0, 0)
        }
        layout.addView(statusText)

        setContentView(ScrollView(this).apply { addView(layout) })
    }

    private fun edit(hint: String, value: String, inputTypeValue: Int): EditText =
        EditText(this).apply {
            this.hint = hint
            this.setText(value)
            this.inputType = inputTypeValue
            setSingleLine(true)
        }

    private fun saveSettings() {
        BridgeSettings.save(
            this,
            BridgeSettings(
                desktopBaseUrl = urlInput.text.toString().trim(),
                bridgeToken = tokenInput.text.toString().trim(),
                semanticLocation = locationInput.text.toString().trim()
            )
        )
    }

    private fun refreshStatus() {
        val status = BridgeRuntimeStatus.load(this)
        val settings = BridgeSettings.load(this)
        statusText.text = buildString {
            appendLine("URL: ${settings.desktopBaseUrl}")
            appendLine("Service: ${if (status.running) "running" else "stopped"}")
            appendLine("Last send: ${status.lastSendAt.ifBlank { "-" }}")
            appendLine("Result: ${if (status.lastOk) "OK" else "FAIL"} HTTP ${status.lastHttpCode}")
            appendLine("Heart: ${status.lastHeartRate.ifBlank { "-" }}")
            appendLine("Motion: ${status.lastMotion.ifBlank { "-" }}")
            appendLine("Error: ${status.lastError.ifBlank { "-" }}")
        }
    }

    private fun findPcBridge() {
        statusText.text = "Scanning Wi-Fi..."
        scope.launch {
            val result = withContext(Dispatchers.IO) { BridgeDiscovery().find() }
            if (result.ok) {
                urlInput.setText(result.baseUrl)
                saveSettings()
                BridgeRuntimeStatus.save(
                    this@MainActivity,
                    BridgeRuntimeStatus(
                        running = BridgeRuntimeStatus.load(this@MainActivity).running,
                        lastOk = true,
                        lastHttpCode = 200,
                        lastError = "Found PC bridge: ${result.baseUrl}"
                    )
                )
                toast("Found ${result.baseUrl}")
            } else {
                BridgeRuntimeStatus.save(
                    this@MainActivity,
                    BridgeRuntimeStatus(
                        running = BridgeRuntimeStatus.load(this@MainActivity).running,
                        lastOk = false,
                        lastError = result.error
                    )
                )
                toast("Not found")
            }
            refreshStatus()
        }
    }

    private fun testBridge() {
        statusText.text = "Testing..."
        scope.launch {
            val result = withContext(Dispatchers.IO) {
                BridgeSender(BridgeSettings.load(this@MainActivity)).status()
            }
            BridgeRuntimeStatus.save(
                this@MainActivity,
                BridgeRuntimeStatus(
                    running = BridgeRuntimeStatus.load(this@MainActivity).running,
                    lastOk = result.ok,
                    lastHttpCode = result.httpCode,
                    lastError = result.error.ifBlank { if (result.ok) "PC bridge reachable" else "test failed" }
                )
            )
            refreshStatus()
            toast(if (result.ok) "PC bridge reachable" else "Test failed: ${result.error}")
        }
    }

    private fun startBridgeAfterTest() {
        statusText.text = "Testing before start..."
        scope.launch {
            val result = withContext(Dispatchers.IO) {
                BridgeSender(BridgeSettings.load(this@MainActivity)).status()
            }

            if (result.ok) {
                startBridge()
                toast("Bridge started")
            } else {
                BridgeRuntimeStatus.save(
                    this@MainActivity,
                    BridgeRuntimeStatus(
                        running = false,
                        lastOk = false,
                        lastHttpCode = result.httpCode,
                        lastError = "Start blocked: ${result.error.ifBlank { "PC bridge not reachable" }}"
                    )
                )
                refreshStatus()
                toast("PC bridge not reachable")
            }
        }
    }

    private fun requestPermissionsIfNeeded(): Boolean {
        val permissions = arrayOf(
            Manifest.permission.BODY_SENSORS,
            Manifest.permission.ACTIVITY_RECOGNITION,
            Manifest.permission.POST_NOTIFICATIONS
        ).filter { checkSelfPermission(it) != PackageManager.PERMISSION_GRANTED }

        if (permissions.isNotEmpty()) {
            ActivityCompat.requestPermissions(this, permissions.toTypedArray(), 7)
            return false
        }

        return true
    }

    override fun onRequestPermissionsResult(requestCode: Int, permissions: Array<out String>, grantResults: IntArray) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults)
        if (requestCode == 7 && startAfterPermissions && grantResults.all { it == PackageManager.PERMISSION_GRANTED }) {
            startBridgeAfterTest()
        } else if (requestCode == 7) {
            toast("Permissions denied")
        }
    }

    private fun startBridge() {
        ContextCompat.startForegroundService(
            this,
            Intent(this, WearBridgeService::class.java)
        )
        refreshStatus()
    }

    private fun toast(text: String) {
        Toast.makeText(this, text, Toast.LENGTH_SHORT).show()
    }
}
