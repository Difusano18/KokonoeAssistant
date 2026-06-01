package dev.kokonoe.wearbridge

data class BridgeConfig(
    val desktopBaseUrl: String = "http://192.168.1.10:8787",
    val bridgeToken: String = "PASTE_TOKEN_FROM_PC",
    val deviceId: String = "galaxy-watch-8-lte",
    val semanticLocation: String = ""
)
