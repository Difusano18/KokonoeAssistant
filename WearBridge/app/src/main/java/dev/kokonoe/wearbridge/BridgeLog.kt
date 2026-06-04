package dev.kokonoe.wearbridge

import android.content.Context
import java.time.Instant

object BridgeLog {
    private const val PREFS = "kokonoe_bridge_log"
    private const val KEY = "entries"
    private const val MAX_ENTRIES = 80

    fun append(context: Context, message: String) {
        val prefs = context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        val current = prefs.getString(KEY, "").orEmpty()
            .lines()
            .filter { it.isNotBlank() }
            .toMutableList()
        current.add("${Instant.now()} ${message.take(220)}")
        while (current.size > MAX_ENTRIES) current.removeAt(0)
        prefs.edit().putString(KEY, current.joinToString("\n")).apply()
    }

    fun tail(context: Context, count: Int = 10): String {
        return context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(KEY, "")
            .orEmpty()
            .lines()
            .filter { it.isNotBlank() }
            .takeLast(count.coerceAtLeast(1))
            .joinToString("\n")
    }
}
