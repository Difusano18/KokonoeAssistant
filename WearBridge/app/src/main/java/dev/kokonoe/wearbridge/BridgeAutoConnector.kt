package dev.kokonoe.wearbridge

import android.content.Context

object BridgeAutoConnector {
    suspend fun reconnectKnownPc(context: Context): BridgeDiscoveryResult {
        val settings = BridgeSettings.load(context)
        val result = BridgeDiscovery().find(preferredPcId = settings.pairedPcId)
        if (!result.ok) return result

        BridgeSettings.save(
            context,
            settings.copy(
                desktopBaseUrl = result.baseUrl,
                pairedPcId = result.pcId.ifBlank { settings.pairedPcId }
            )
        )
        return result
    }

    suspend fun pairCurrentUrl(context: Context): BridgeSendResult {
        val settings = BridgeSettings.load(context)
        val result = BridgeSender(settings).pair()
        if (!result.ok) return result

        BridgeSettings.save(
            context,
            settings.copy(
                bridgeToken = result.token,
                pairedPcId = result.pcId,
                desktopBaseUrl = settings.desktopBaseUrl
            )
        )
        return result
    }
}
