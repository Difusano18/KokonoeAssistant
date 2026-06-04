package dev.kokonoe.wearbridge

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.net.URLEncoder
import java.util.concurrent.TimeUnit
import kotlin.math.min
import kotlin.random.Random

class BridgeSender(private val settings: BridgeSettings) {
    private val client = OkHttpClient.Builder()
        .connectTimeout(5, TimeUnit.SECONDS)
        .readTimeout(5, TimeUnit.SECONDS)
        .writeTimeout(5, TimeUnit.SECONDS)
        .build()

    suspend fun send(sample: WearableSample): BridgeSendResult = withContext(Dispatchers.IO) {
        executeWithRetry("send") {
            val body = sample.toJson().toRequestBody("application/json; charset=utf-8".toMediaType())
            val request = Request.Builder()
                .url("${settings.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/sample")
                .header("X-Koko-Bridge-Token", settings.bridgeToken)
                .post(body)
                .build()

            client.newCall(request).execute().use {
                BridgeSendResult(ok = it.isSuccessful, httpCode = it.code, error = if (it.isSuccessful) "" else it.message)
            }
        }
    }

    suspend fun sendJson(json: String): BridgeSendResult = withContext(Dispatchers.IO) {
        executeWithRetry("send_cached") {
            val body = json.toRequestBody("application/json; charset=utf-8".toMediaType())
            val request = Request.Builder()
                .url("${settings.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/sample")
                .header("X-Koko-Bridge-Token", settings.bridgeToken)
                .post(body)
                .build()

            client.newCall(request).execute().use {
                BridgeSendResult(ok = it.isSuccessful, httpCode = it.code, error = if (it.isSuccessful) "" else it.message)
            }
        }
    }

    suspend fun sendJsonBatch(samples: List<String>): BridgeSendResult = withContext(Dispatchers.IO) {
        if (samples.isEmpty()) return@withContext BridgeSendResult(ok = true)
        executeWithRetry("send_batch") {
            val body = samples.joinToString(prefix = "[", postfix = "]", separator = ",")
                .toRequestBody("application/json; charset=utf-8".toMediaType())
            val request = Request.Builder()
                .url("${settings.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/samples")
                .header("X-Koko-Bridge-Token", settings.bridgeToken)
                .post(body)
                .build()

            client.newCall(request).execute().use {
                BridgeSendResult(ok = it.isSuccessful, httpCode = it.code, error = if (it.isSuccessful) "" else it.message)
            }
        }
    }

    suspend fun status(): BridgeSendResult = withContext(Dispatchers.IO) {
        executeWithRetry("status", maxAttempts = 2, initialDelayMs = 350L) {
            val request = Request.Builder()
                .url("${settings.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/status")
                .get()
                .build()
            client.newCall(request).execute().use {
                val body = it.body?.string().orEmpty()
                val json = body.takeIf { text -> text.isNotBlank() }?.let { text -> runCatching { JSONObject(text) }.getOrNull() }
                BridgeSendResult(
                    ok = it.isSuccessful,
                    httpCode = it.code,
                    error = if (it.isSuccessful) "" else it.message,
                    pcId = json?.optString("pcId").orEmpty(),
                    pcName = json?.optString("pcName").orEmpty(),
                    knownBaseUrls = readUrls(json).joinToString("\n")
                )
            }
        }
    }

    suspend fun pair(): BridgeSendResult = withContext(Dispatchers.IO) {
        executeWithRetry("pair", maxAttempts = 4, initialDelayMs = 700L) {
            val payload = JSONObject()
                .put("deviceId", settings.deviceId)
                .put("appVersion", "wearbridge-1")
                .toString()
                .toRequestBody("application/json; charset=utf-8".toMediaType())
            val request = Request.Builder()
                .url("${settings.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/pair")
                .post(payload)
                .build()

            client.newCall(request).execute().use {
                val body = it.body?.string().orEmpty()
                val json = body.takeIf { text -> text.isNotBlank() }?.let { text -> runCatching { JSONObject(text) }.getOrNull() }
                val token = json?.optString("token").orEmpty()
                BridgeSendResult(
                    ok = it.isSuccessful && token.isNotBlank(),
                    httpCode = it.code,
                    error = if (it.isSuccessful && token.isNotBlank()) "" else it.message.ifBlank { "pairing_failed" },
                    pcId = json?.optString("pcId").orEmpty(),
                    pcName = json?.optString("pcName").orEmpty(),
                    token = token,
                    knownBaseUrls = readUrls(json).joinToString("\n")
                )
            }
        }
    }

    suspend fun nextCommand(): BridgeSendResult = withContext(Dispatchers.IO) {
        executeWithRetry("command", maxAttempts = 2, initialDelayMs = 350L) {
            val deviceId = URLEncoder.encode(settings.deviceId, Charsets.UTF_8.name())
            val request = Request.Builder()
                .url("${settings.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/command?deviceId=$deviceId")
                .header("X-Koko-Bridge-Token", settings.bridgeToken)
                .get()
                .build()

            client.newCall(request).execute().use {
                val body = it.body?.string().orEmpty()
                val json = body.takeIf { text -> text.isNotBlank() }?.let { text -> runCatching { JSONObject(text) }.getOrNull() }
                BridgeSendResult(
                    ok = it.isSuccessful,
                    httpCode = it.code,
                    error = if (it.isSuccessful) "" else it.message,
                    commandAction = json?.optString("action").orEmpty(),
                    commandId = json?.optString("commandId").orEmpty()
                )
            }
        }
    }

    suspend fun ackCommand(commandId: String, action: String, ok: Boolean, detail: String = ""): BridgeSendResult = withContext(Dispatchers.IO) {
        executeWithRetry("command_ack", maxAttempts = 2, initialDelayMs = 350L) {
            val payload = JSONObject()
                .put("commandId", commandId)
                .put("action", action)
                .put("ok", ok)
                .put("detail", detail.take(180))
                .toString()
                .toRequestBody("application/json; charset=utf-8".toMediaType())
            val request = Request.Builder()
                .url("${settings.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/command/ack")
                .header("X-Koko-Bridge-Token", settings.bridgeToken)
                .post(payload)
                .build()

            client.newCall(request).execute().use {
                BridgeSendResult(ok = it.isSuccessful, httpCode = it.code, error = if (it.isSuccessful) "" else it.message)
            }
        }
    }

    private suspend fun executeWithRetry(
        operation: String,
        maxAttempts: Int = 3,
        initialDelayMs: Long = 500L,
        block: () -> BridgeSendResult
    ): BridgeSendResult {
        var last = BridgeSendResult(ok = false, error = "$operation failed before first attempt", attempts = 0)
        repeat(maxAttempts.coerceAtLeast(1)) { index ->
            val attempt = index + 1
            val result = runCatching { block() }.getOrElse {
                BridgeSendResult(ok = false, error = it.javaClass.simpleName + ": " + (it.message ?: "$operation failed"))
            }.copy(attempts = attempt)
            last = result

            if (result.ok || !shouldRetry(result) || attempt >= maxAttempts) {
                return result
            }

            val jitter = Random.nextLong(0, 180)
            val delayMs = min(initialDelayMs * (1L shl index), 8_000L) + jitter
            kotlinx.coroutines.delay(delayMs)
        }
        return last
    }

    private fun shouldRetry(result: BridgeSendResult): Boolean {
        if (result.httpCode == 401 || result.httpCode == 403) return false
        if (result.httpCode == 400 || result.httpCode == 404) return false
        return result.httpCode == 0 || result.httpCode == 408 || result.httpCode == 429 || result.httpCode >= 500
    }

    private fun readUrls(json: JSONObject?): List<String> {
        if (json == null) return emptyList()
        val result = mutableListOf<String>()
        for (name in listOf("urls", "externalUrls")) {
            val array = json.optJSONArray(name) ?: continue
            for (i in 0 until array.length()) {
                val value = array.optString(i).trim().trimEnd('/')
                if (value.isNotBlank()) result.add(value)
            }
        }
        return result.distinct()
    }
}
