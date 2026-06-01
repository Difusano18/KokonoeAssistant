package dev.kokonoe.wearbridge

data class WearableSample(
    val timestampUtc: String,
    val deviceId: String,
    val source: String = "wear-os-bridge",
    val heartRateBpm: Double? = null,
    val ibiMs: Double? = null,
    val hrvRmssdMs: Double? = null,
    val spO2Percent: Double? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
    val locationAccuracyM: Double? = null,
    val semanticLocation: String = "",
    val motion: Double? = null,
    val onWrist: Boolean? = null,
    val activity: String = "",
    val batteryPercent: Double? = null,
    val charging: Boolean? = null,
    val note: String = ""
)
