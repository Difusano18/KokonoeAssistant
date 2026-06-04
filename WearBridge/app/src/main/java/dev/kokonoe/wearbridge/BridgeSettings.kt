package dev.kokonoe.wearbridge

import android.content.Context

data class BridgeSettings(
    val desktopBaseUrl: String = BridgeConfig().desktopBaseUrl,
    val bridgeToken: String = BridgeConfig().bridgeToken,
    val deviceId: String = BridgeConfig().deviceId,
    val semanticLocation: String = BridgeConfig().semanticLocation,
    val autoStart: Boolean = false,
    val pairedPcId: String = "",
    val lastSuccessfulBaseUrl: String = "",
    val knownBaseUrls: String = ""
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
                semanticLocation = prefs.getString("semanticLocation", defaults.semanticLocation).orEmpty(),
                autoStart = prefs.getBoolean("autoStart", false),
                pairedPcId = prefs.getString("pairedPcId", "").orEmpty(),
                lastSuccessfulBaseUrl = prefs.getString("lastSuccessfulBaseUrl", "").orEmpty(),
                knownBaseUrls = prefs.getString("knownBaseUrls", "").orEmpty()
            )
        }

        fun save(context: Context, settings: BridgeSettings) {
            context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
                .edit()
                .putString("desktopBaseUrl", settings.desktopBaseUrl.trim())
                .putString("bridgeToken", settings.bridgeToken.trim())
                .putString("deviceId", settings.deviceId.trim())
                .putString("semanticLocation", settings.semanticLocation.trim())
                .putBoolean("autoStart", settings.autoStart)
                .putString("pairedPcId", settings.pairedPcId.trim())
                .putString("lastSuccessfulBaseUrl", settings.lastSuccessfulBaseUrl.trim())
                .putString("knownBaseUrls", settings.knownBaseUrls.trim())
                .apply()
        }

        fun candidateUrls(settings: BridgeSettings): List<String> {
            return sequenceOf(
                settings.lastSuccessfulBaseUrl,
                settings.desktopBaseUrl,
                settings.knownBaseUrls
            )
                .flatMap { it.split(',', ';', '\n').asSequence() }
                .map { normalizeBaseUrl(it) }
                .filter { it.isNotBlank() }
                .distinct()
                .toList()
        }

        fun mergeKnownUrls(settings: BridgeSettings, url: String): String {
            return (candidateUrls(settings) + normalizeBaseUrl(url))
                .filter { it.isNotBlank() }
                .distinct()
                .take(8)
                .joinToString("\n")
        }

        private fun normalizeBaseUrl(value: String): String {
            val trimmed = value.trim().trimEnd('/')
            if (trimmed.isBlank()) return ""
            return if (trimmed.startsWith("http://") || trimmed.startsWith("https://")) trimmed else "http://$trimmed"
        }
    }
}
