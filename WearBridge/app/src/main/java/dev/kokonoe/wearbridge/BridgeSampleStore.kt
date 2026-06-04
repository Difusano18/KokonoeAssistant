package dev.kokonoe.wearbridge

import android.content.Context

class BridgeSampleStore(private val context: Context) {
    private val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun enqueue(sample: WearableSample) {
        val samples = loadMutable()
        samples.add(sample.toJson())
        while (samples.size > MAX_SAMPLES) samples.removeAt(0)
        save(samples)
    }

    fun peekBatch(maxCount: Int = 24): List<String> =
        loadMutable().take(maxCount.coerceAtLeast(1))

    fun removeSent(count: Int) {
        if (count <= 0) return
        val samples = loadMutable()
        repeat(count.coerceAtMost(samples.size)) { samples.removeAt(0) }
        save(samples)
    }

    fun count(): Int = loadMutable().size

    fun clear() {
        prefs.edit().remove(KEY).apply()
    }

    private fun loadMutable(): MutableList<String> =
        prefs.getString(KEY, "")
            .orEmpty()
            .lines()
            .filter { it.isNotBlank() }
            .toMutableList()

    private fun save(samples: List<String>) {
        prefs.edit().putString(KEY, samples.joinToString("\n")).apply()
    }

    companion object {
        private const val PREFS = "kokonoe_bridge_sample_store"
        private const val KEY = "jsonl"
        private const val MAX_SAMPLES = 360
    }
}
