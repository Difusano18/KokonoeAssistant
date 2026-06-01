package dev.kokonoe.wearbridge

import android.content.Context

data class BridgeRuntimeStatus(
    val running: Boolean = false,
    val lastSendAt: String = "",
    val lastOk: Boolean = false,
    val lastHttpCode: Int = 0,
    val lastError: String = "",
    val lastHeartRate: String = "",
    val lastMotion: String = ""
) {
    companion object {
        private const val PREFS = "kokonoe_bridge_runtime"

        fun load(context: Context): BridgeRuntimeStatus {
            val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            return BridgeRuntimeStatus(
                running = prefs.getBoolean("running", false),
                lastSendAt = prefs.getString("lastSendAt", "").orEmpty(),
                lastOk = prefs.getBoolean("lastOk", false),
                lastHttpCode = prefs.getInt("lastHttpCode", 0),
                lastError = prefs.getString("lastError", "").orEmpty(),
                lastHeartRate = prefs.getString("lastHeartRate", "").orEmpty(),
                lastMotion = prefs.getString("lastMotion", "").orEmpty()
            )
        }

        fun save(context: Context, status: BridgeRuntimeStatus) {
            context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .putBoolean("running", status.running)
                .putString("lastSendAt", status.lastSendAt)
                .putBoolean("lastOk", status.lastOk)
                .putInt("lastHttpCode", status.lastHttpCode)
                .putString("lastError", status.lastError.take(240))
                .putString("lastHeartRate", status.lastHeartRate)
                .putString("lastMotion", status.lastMotion)
                .apply()
        }
    }
}
