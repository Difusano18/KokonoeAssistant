package dev.kokonoe.wearbridge

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.concurrent.TimeUnit

class BridgeSender(private val config: BridgeConfig) {
    private val client = OkHttpClient.Builder()
        .connectTimeout(4, TimeUnit.SECONDS)
        .readTimeout(4, TimeUnit.SECONDS)
        .build()

    suspend fun send(sample: WearableSample): Boolean = withContext(Dispatchers.IO) {
        val body = sample.toJson().toRequestBody("application/json; charset=utf-8".toMediaType())
        val request = Request.Builder()
            .url("${config.desktopBaseUrl.trimEnd('/')}/api/wearable/v1/sample")
            .header("X-Koko-Bridge-Token", config.bridgeToken)
            .post(body)
            .build()

        client.newCall(request).execute().use { it.isSuccessful }
    }
}
