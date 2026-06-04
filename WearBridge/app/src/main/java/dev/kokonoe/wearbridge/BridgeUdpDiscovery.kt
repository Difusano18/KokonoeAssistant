package dev.kokonoe.wearbridge

import android.util.Log
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress

object BridgeUdpDiscovery {
    private const val TAG = "KokonoeBridgeUdp"
    private const val PROBE = "KOKONOE_WEARABLE_DISCOVER_V1"

    suspend fun discover(port: Int, prefixes: List<String>, timeoutMs: Int = 450): List<String> = withContext(Dispatchers.IO) {
        val found = linkedSetOf<String>()
        runCatching {
            DatagramSocket().use { socket ->
                socket.broadcast = true
                socket.soTimeout = timeoutMs
                val payload = PROBE.toByteArray(Charsets.UTF_8)
                val targets = (listOf("255.255.255.255") + prefixes.map { "$it.255" }).distinct()
                for (target in targets) {
                    runCatching {
                        socket.send(DatagramPacket(payload, payload.size, InetAddress.getByName(target), port))
                    }.onFailure {
                        Log.e(TAG, "UDP probe send failed $target", it)
                    }
                }

                val deadline = System.currentTimeMillis() + timeoutMs
                val buffer = ByteArray(2048)
                while (System.currentTimeMillis() < deadline) {
                    val packet = DatagramPacket(buffer, buffer.size)
                    val received = runCatching {
                        socket.receive(packet)
                        packet
                    }.getOrNull() ?: break
                    val text = String(received.data, 0, received.length, Charsets.UTF_8)
                    val json = runCatching { JSONObject(text) }.getOrNull() ?: continue
                    if (json.optString("bridge") != "kokonoe-wearable-v1") continue
                    val url = json.optString("baseUrl").ifBlank {
                        "http://${received.address.hostAddress}:${json.optInt("port", port)}"
                    }.trimEnd('/')
                    if (url.isNotBlank()) found.add(url)
                }
            }
        }.onFailure {
            Log.e(TAG, "UDP discovery failed", it)
        }
        found.toList()
    }
}
