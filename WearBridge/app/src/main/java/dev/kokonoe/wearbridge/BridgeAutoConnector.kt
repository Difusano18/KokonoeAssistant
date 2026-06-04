package dev.kokonoe.wearbridge

import android.content.Context

object BridgeAutoConnector {
    suspend fun reconnectKnownPc(context: Context): BridgeDiscoveryResult {
        val settings = BridgeSettings.load(context)
        val candidates = BridgeSettings.candidateUrls(settings)
        val result = BridgeDiscovery(context).find(
            preferredPcId = settings.pairedPcId,
            preferredBaseUrl = settings.lastSuccessfulBaseUrl,
            candidateBaseUrls = candidates,
            scanSubnet = true
        )
        if (!result.ok) return result

        BridgeSettings.save(
            context,
            settings.copy(
                desktopBaseUrl = result.baseUrl,
                pairedPcId = result.pcId.ifBlank { settings.pairedPcId },
                lastSuccessfulBaseUrl = result.baseUrl,
                knownBaseUrls = BridgeSettings.mergeKnownUrls(
                    settings.copy(knownBaseUrls = "${settings.knownBaseUrls}\n${result.knownBaseUrls}"),
                    result.baseUrl
                )
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
                desktopBaseUrl = settings.desktopBaseUrl,
                lastSuccessfulBaseUrl = settings.desktopBaseUrl,
                knownBaseUrls = BridgeSettings.mergeKnownUrls(
                    settings.copy(knownBaseUrls = "${settings.knownBaseUrls}\n${result.knownBaseUrls}"),
                    settings.desktopBaseUrl
                )
            )
        )
        return result
    }

    suspend fun discoverAndPair(context: Context): BridgeSendResult {
        val settings = BridgeSettings.load(context)
        val candidates = BridgeSettings.candidateUrls(settings)
        val discovered = BridgeDiscovery(context).find(
            preferredPcId = settings.pairedPcId,
            preferredBaseUrl = settings.lastSuccessfulBaseUrl,
            candidateBaseUrls = candidates,
            scanSubnet = true
        )
        if (!discovered.ok) {
            BridgeLog.append(context, "discover failed: ${discovered.error}")
            return BridgeSendResult(ok = false, error = discovered.error)
        }

        BridgeSettings.save(
            context,
            settings.copy(
                desktopBaseUrl = discovered.baseUrl,
                pairedPcId = discovered.pcId.ifBlank { settings.pairedPcId },
                lastSuccessfulBaseUrl = discovered.baseUrl,
                knownBaseUrls = BridgeSettings.mergeKnownUrls(
                    settings.copy(knownBaseUrls = "${settings.knownBaseUrls}\n${discovered.knownBaseUrls}"),
                    discovered.baseUrl
                ),
                autoStart = true
            )
        )
        BridgeLog.append(context, "discovered ${discovered.pcName.ifBlank { discovered.baseUrl }}")
        return pairCurrentUrl(context)
    }
}
