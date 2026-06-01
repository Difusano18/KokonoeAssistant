package dev.kokonoe.wearbridge

private fun String.escapeJson(): String =
    replace("\\", "\\\\").replace("\"", "\\\"")

fun WearableSample.toJson(): String {
    fun field(name: String, value: String?) = "\"$name\":${value ?: "null"}"
    fun stringField(name: String, value: String) = field(name, "\"${value.escapeJson()}\"")
    fun doubleField(name: String, value: Double?) = field(name, value?.toString())
    fun boolField(name: String, value: Boolean?) = field(name, value?.toString())

    return buildString {
        append("{")
        append(stringField("timestampUtc", timestampUtc)); append(",")
        append(stringField("deviceId", deviceId)); append(",")
        append(stringField("source", source)); append(",")
        append(doubleField("heartRateBpm", heartRateBpm)); append(",")
        append(doubleField("ibiMs", ibiMs)); append(",")
        append(doubleField("hrvRmssdMs", hrvRmssdMs)); append(",")
        append(doubleField("spO2Percent", spO2Percent)); append(",")
        append(doubleField("latitude", latitude)); append(",")
        append(doubleField("longitude", longitude)); append(",")
        append(doubleField("locationAccuracyM", locationAccuracyM)); append(",")
        append(stringField("semanticLocation", semanticLocation)); append(",")
        append(doubleField("motion", motion)); append(",")
        append(boolField("onWrist", onWrist)); append(",")
        append(stringField("activity", activity)); append(",")
        append(doubleField("batteryPercent", batteryPercent)); append(",")
        append(boolField("charging", charging)); append(",")
        append(stringField("note", note))
        append("}")
    }
}
