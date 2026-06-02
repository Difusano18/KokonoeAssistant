package dev.kokonoe.wearbridge

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONObject
import java.util.concurrent.TimeUnit

class BridgeSender(private val settings: BridgeSettings) {
    private val client = OkHttpClient.Builder()
        .connectTimeout(4, TimeUnit.SECONDS)
        .readTimeout(4, TimeUnit.SECONDS)
        .build()

    suspend fun send(sample: WearableSample): BridgeSendResult = withContext(Dispatchers.IO) {
        runCatching {
            val body = sample.toJson().toRequestBody("application/json; charset=utf-8".toMediaType())
            val request = Request.Builder()
                .url("${settings.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/sample")
                .header("X-Koko-Bridge-Token", settings.bridgeToken)
                .post(body)
                .build()

            client.newCall(request).execute().use {
                BridgeSendResult(ok = it.isSuccessful, httpCode = it.code, error = if (it.isSuccessful) "" else it.message)
            }
        }.getOrElse {
            BridgeSendResult(ok = false, error = it.javaClass.simpleName + ": " + (it.message ?: "send failed"))
        }
    }

    suspend fun status(): BridgeSendResult = withContext(Dispatchers.IO) {
        runCatching {
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
                    pcName = json?.optString("pcName").orEmpty()
                )
            }
        }.getOrElse {
            BridgeSendResult(ok = false, error = it.javaClass.simpleName + ": " + (it.message ?: "status failed"))
        }
    }

    suspend fun pair(): BridgeSendResult = withContext(Dispatchers.IO) {
        runCatching {
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
                    token = token
                )
            }
        }.getOrElse {
            BridgeSendResult(ok = false, error = it.javaClass.simpleName + ": " + (it.message ?: "pair failed"))
        }
    }
}
