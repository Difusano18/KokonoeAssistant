package dev.kokonoe.wearbridge

import android.content.Context

data class BridgeRuntimeStatus(
    val running: Boolean = false,
    val lastSendAt: String = "",
    val lastOk: Boolean = false,
    val lastHttpCode: Int = 0,
    val lastError: String = "",
    val lastHeartRate: String = "",
    val heartStatus: String = "",
    val lastMotion: String = "",
    val phase: String = "",
    val pairedPcId: String = "",
    val pcName: String = "",
    val reconnectCount: Int = 0,
    val nextRetryAt: String = "",
    val queuedSamples: Int = 0,
    val activeBaseUrl: String = "",
    val knownUrlCount: Int = 0,
    val lastAttempts: Int = 0,
    val logTail: String = ""
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
                heartStatus = prefs.getString("heartStatus", "").orEmpty(),
                lastMotion = prefs.getString("lastMotion", "").orEmpty(),
                phase = prefs.getString("phase", "").orEmpty(),
                pairedPcId = prefs.getString("pairedPcId", "").orEmpty(),
                pcName = prefs.getString("pcName", "").orEmpty(),
                reconnectCount = prefs.getInt("reconnectCount", 0),
                nextRetryAt = prefs.getString("nextRetryAt", "").orEmpty(),
                queuedSamples = prefs.getInt("queuedSamples", 0),
                activeBaseUrl = prefs.getString("activeBaseUrl", "").orEmpty(),
                knownUrlCount = prefs.getInt("knownUrlCount", 0),
                lastAttempts = prefs.getInt("lastAttempts", 0),
                logTail = prefs.getString("logTail", "").orEmpty()
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
                .putString("heartStatus", status.heartStatus.take(160))
                .putString("lastMotion", status.lastMotion)
                .putString("phase", status.phase)
                .putString("pairedPcId", status.pairedPcId)
                .putString("pcName", status.pcName)
                .putInt("reconnectCount", status.reconnectCount)
                .putString("nextRetryAt", status.nextRetryAt)
                .putInt("queuedSamples", status.queuedSamples)
                .putString("activeBaseUrl", status.activeBaseUrl)
                .putInt("knownUrlCount", status.knownUrlCount)
                .putInt("lastAttempts", status.lastAttempts)
                .putString("logTail", status.logTail.take(1200))
                .apply()
        }
    }
}
