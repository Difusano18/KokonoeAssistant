package dev.kokonoe.wearbridge

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.content.ContextCompat

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val action = intent.action ?: return
        if (action !in setOf(Intent.ACTION_BOOT_COMPLETED, Intent.ACTION_LOCKED_BOOT_COMPLETED, Intent.ACTION_MY_PACKAGE_REPLACED)) return
        if (!BridgeSettings.load(context).autoStart) return
        BridgeLog.append(context, "boot receiver starting service: $action")
        ContextCompat.startForegroundService(context, Intent(context, WearBridgeService::class.java))
    }
}
