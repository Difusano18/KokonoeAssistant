package dev.kokonoe.wearbridge

data class BridgeSendResult(
    val ok: Boolean,
    val httpCode: Int = 0,
    val error: String = ""
)
