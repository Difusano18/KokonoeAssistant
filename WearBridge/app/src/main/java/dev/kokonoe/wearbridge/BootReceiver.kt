package dev.kokonoe.wearbridge

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.content.ContextCompat

class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != Intent.ACTION_BOOT_COMPLETED) return
        if (!BridgeSettings.load(context).autoStart) return
        ContextCompat.startForegroundService(context, Intent(context, WearBridgeService::class.java))
    }
}
