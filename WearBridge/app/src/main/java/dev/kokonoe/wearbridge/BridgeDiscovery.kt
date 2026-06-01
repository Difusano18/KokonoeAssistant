package dev.kokonoe.wearbridge

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import java.net.NetworkInterface
import java.util.concurrent.TimeUnit

class BridgeDiscovery {
    private val client = OkHttpClient.Builder()
        .connectTimeout(450, TimeUnit.MILLISECONDS)
        .readTimeout(650, TimeUnit.MILLISECONDS)
        .build()

    suspend fun find(port: Int = 8787): BridgeDiscoveryResult = withContext(Dispatchers.IO) {
        val prefixes = localPrefixes()
        if (prefixes.isEmpty()) {
            return@withContext BridgeDiscoveryResult(false, error = "No Wi-Fi/LAN IPv4 address found on watch")
        }

        coroutineScope {
            val jobs = prefixes.flatMap { prefix ->
                (1..254).map { host ->
                    async {
                        val baseUrl = "http://$prefix.$host:$port"
                        if (probe(baseUrl)) baseUrl else null
                    }
                }
            }
            val found = jobs.awaitAll().filterNotNull().firstOrNull()
            if (found != null) BridgeDiscoveryResult(true, baseUrl = found)
            else BridgeDiscoveryResult(false, error = "No Kokonoe bridge found on local /24 subnet")
        }
    }

    private fun probe(baseUrl: String): Boolean {
        val request = Request.Builder()
            .url("${baseUrl.trimEnd('/')}/api/wearable/v1/status")
            .get()
            .build()

        return runCatching {
            client.newCall(request).execute().use { response ->
                response.isSuccessful && (response.body?.string()?.contains("kokonoe-wearable-v1") == true)
            }
        }.getOrDefault(false)
    }

    private fun localPrefixes(): List<String> {
        val result = mutableSetOf<String>()
        val interfaces = NetworkInterface.getNetworkInterfaces()?.toList().orEmpty()
        for (iface in interfaces) {
            if (!iface.isUp || iface.isLoopback) continue
            for (addr in iface.inetAddresses.toList()) {
                val host = addr.hostAddress ?: continue
                val parts = host.split(".")
                if (parts.size == 4 && parts[0] != "127" && parts[0] != "169") {
                    result.add(parts.take(3).joinToString("."))
                }
            }
        }
        return result.toList()
    }
}

data class BridgeDiscoveryResult(
    val ok: Boolean,
    val baseUrl: String = "",
    val error: String = ""
)
