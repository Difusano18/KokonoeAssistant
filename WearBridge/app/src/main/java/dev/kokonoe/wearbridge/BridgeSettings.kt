package dev.kokonoe.wearbridge

import android.content.Context

data class BridgeSettings(
    val desktopBaseUrl: String = BridgeConfig().desktopBaseUrl,
    val bridgeToken: String = BridgeConfig().bridgeToken,
    val deviceId: String = BridgeConfig().deviceId,
    val semanticLocation: String = BridgeConfig().semanticLocation
) {
    companion object {
        private const val PREFS = "kokonoe_bridge_settings"

        fun load(context: Context): BridgeSettings {
            val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            val defaults = BridgeConfig()
            return BridgeSettings(
                desktopBaseUrl = prefs.getString("desktopBaseUrl", defaults.desktopBaseUrl).orEmpty().ifBlank { defaults.desktopBaseUrl },
                bridgeToken = prefs.getString("bridgeToken", defaults.bridgeToken).orEmpty().ifBlank { defaults.bridgeToken },
                deviceId = prefs.getString("deviceId", defaults.deviceId).orEmpty().ifBlank { defaults.deviceId },
                semanticLocation = prefs.getString("semanticLocation", defaults.semanticLocation).orEmpty()
            )
        }

        fun save(context: Context, settings: BridgeSettings) {
            context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .putString("desktopBaseUrl", settings.desktopBaseUrl.trim())
                .putString("bridgeToken", settings.bridgeToken.trim())
                .putString("deviceId", settings.deviceId.trim())
                .putString("semanticLocation", settings.semanticLocation.trim())
                .apply()
        }
    }
}
