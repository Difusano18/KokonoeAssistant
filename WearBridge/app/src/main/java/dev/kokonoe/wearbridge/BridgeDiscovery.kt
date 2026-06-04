package dev.kokonoe.wearbridge

import android.content.Context
import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.json.JSONObject
import java.net.NetworkInterface
import java.util.concurrent.TimeUnit

class BridgeDiscovery(private val context: Context? = null) {
    private val tag = "KokonoeBridgeDiscovery"
    private val client = OkHttpClient.Builder()
        .connectTimeout(300, TimeUnit.MILLISECONDS)
        .readTimeout(450, TimeUnit.MILLISECONDS)
        .build()

    suspend fun find(
        port: Int = 8787,
        preferredPcId: String = "",
        preferredBaseUrl: String = "",
        candidateBaseUrls: List<String> = emptyList(),
        scanSubnet: Boolean = true
    ): BridgeDiscoveryResult = withContext(Dispatchers.IO) {
        val fastCandidates = (listOf(preferredBaseUrl) + candidateBaseUrls)
            .map { it.trim().trimEnd('/') }
            .filter { it.isNotBlank() }
            .distinct()

        for (baseUrl in fastCandidates) {
            probe(baseUrl, preferredPcId, "configured")?.let { return@withContext it.copy(fromCache = true) }
        }

        val nsdUrls = context?.let { BridgeNsdDiscovery.discover(it, port) }.orEmpty()
        for (baseUrl in nsdUrls) {
            probe(baseUrl, preferredPcId, "nsd")?.let { return@withContext it.copy(fromNsd = true) }
        }

        if (!scanSubnet) {
            return@withContext BridgeDiscoveryResult(false, error = "Configured PC URLs and NSD did not answer; subnet scan disabled until pairing is locked")
        }

        val prefixes = localPrefixes()
        if (prefixes.isEmpty()) {
            return@withContext BridgeDiscoveryResult(false, error = "No Wi-Fi/LAN IPv4 address found on watch")
        }

        val udpUrls = BridgeUdpDiscovery.discover(port, prefixes)
        for (baseUrl in udpUrls) {
            probe(baseUrl, preferredPcId, "udp")?.let { return@withContext it.copy(fromUdp = true) }
        }

        coroutineScope {
            val candidates = prefixes.flatMap { prefix ->
                commonHosts(prefix, port) + (1..254).map { host -> "http://$prefix.$host:$port" }
            }.distinct()

            for (chunk in candidates.chunked(32)) {
                val jobs = chunk.map { baseUrl -> async { probe(baseUrl, preferredPcId, "subnet") } }
                val found = jobs.mapNotNull { it.await() }.firstOrNull()
                if (found != null) return@coroutineScope found
            }

            BridgeDiscoveryResult(false, error = "No matching Kokonoe bridge found by configured IP, NSD, UDP, or local /24 subnet")
        }
    }

    private fun probe(baseUrl: String, preferredPcId: String, source: String): BridgeDiscoveryResult? {
        val request = Request.Builder()
            .url("${baseUrl.trimEnd('/')}/api/wearable/v1/status")
            .get()
            .build()

        return runCatching {
            client.newCall(request).execute().use { response ->
                val body = response.body?.string().orEmpty()
                if (!response.isSuccessful || !body.contains("kokonoe-wearable-v1")) return@use null
                val json = runCatching { JSONObject(body) }.getOrNull()
                val pcId = json?.optString("pcId").orEmpty()
                val pcName = json?.optString("pcName").orEmpty()
                if (preferredPcId.isNotBlank() && pcId != preferredPcId) {
                    Log.d(tag, "Rejected $source $baseUrl pcId=$pcId expected=$preferredPcId")
                    return@use null
                }
                Log.d(tag, "Found $source $baseUrl pcId=$pcId")
                BridgeDiscoveryResult(
                    true,
                    baseUrl = baseUrl,
                    pcId = pcId,
                    pcName = pcName,
                    knownBaseUrls = readUrls(json).joinToString("\n")
                )
            }
        }.onFailure {
            Log.d(tag, "Probe failed $source $baseUrl: ${it.javaClass.simpleName}: ${it.message}")
        }.getOrNull()
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

    private fun commonHosts(prefix: String, port: Int): List<String> =
        listOf(1, 2, 10, 20, 50, 100, 101, 150, 200, 254).map { "http://$prefix.$it:$port" }
}

data class BridgeDiscoveryResult(
    val ok: Boolean,
    val baseUrl: String = "",
    val pcId: String = "",
    val pcName: String = "",
    val error: String = "",
    val fromCache: Boolean = false,
    val fromNsd: Boolean = false,
    val fromUdp: Boolean = false,
    val knownBaseUrls: String = ""
)
