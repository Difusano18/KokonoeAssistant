package dev.kokonoe.wearbridge

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Bundle
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat

class MainActivity : Activity() {
    private var startAfterPermissions = false

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        val layout = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            setPadding(24, 24, 24, 24)
        }
        val title = TextView(this).apply {
            text = "Kokonoe Wear Bridge"
            textSize = 18f
        }
        val hint = TextView(this).apply {
            text = "Edit BridgeConfig.kt with PC IP and token, grant sensors, then start bridge."
            textSize = 12f
        }
        val start = Button(this).apply {
            text = "Start"
            setOnClickListener {
                startAfterPermissions = true
                if (requestPermissionsIfNeeded()) startBridge()
            }
        }
        val stop = Button(this).apply {
            text = "Stop"
            setOnClickListener { stopService(Intent(this@MainActivity, WearBridgeService::class.java)) }
        }

        layout.addView(title)
        layout.addView(hint)
        layout.addView(start)
        layout.addView(stop)
        setContentView(layout)

        requestPermissionsIfNeeded()
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
            startBridge()
        }
    }

    private fun startBridge() {
        ContextCompat.startForegroundService(
            this,
            Intent(this, WearBridgeService::class.java)
        )
    }
}
