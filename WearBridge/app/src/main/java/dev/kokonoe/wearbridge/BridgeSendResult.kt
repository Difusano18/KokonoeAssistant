package dev.kokonoe.wearbridge

data class BridgeSendResult(
    val ok: Boolean,
    val httpCode: Int = 0,
    val error: String = "",
    val pcId: String = "",
    val pcName: String = "",
    val token: String = "",
    val attempts: Int = 1,
    val commandAction: String = "",
    val commandId: String = "",
    val knownBaseUrls: String = ""
)
