package dev.kokonoe.wearbridge

import android.content.Context
import android.net.nsd.NsdManager
import android.net.nsd.NsdServiceInfo
import android.net.wifi.WifiManager
import android.os.Handler
import android.os.Looper
import android.util.Log
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume

object BridgeNsdDiscovery {
    private const val TAG = "KokonoeBridgeNsd"
    private const val SERVICE_TYPE = "_kokonoe-wearable._tcp."

    suspend fun discover(context: Context, fallbackPort: Int, timeoutMs: Long = 2_200L): List<String> =
        suspendCancellableCoroutine { cont ->
            val appContext = context.applicationContext
            val nsd = appContext.getSystemService(Context.NSD_SERVICE) as? NsdManager
            if (nsd == null) {
                cont.resume(emptyList())
                return@suspendCancellableCoroutine
            }

            val wifi = appContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
            val multicastLock = runCatching {
                wifi?.createMulticastLock("kokonoe_wearbridge_nsd")?.apply {
                    setReferenceCounted(false)
                    acquire()
                }
            }.getOrNull()
            val found = linkedSetOf<String>()
            val handler = Handler(Looper.getMainLooper())
            var finished = false
            lateinit var listener: NsdManager.DiscoveryListener

            fun finish(reason: String) {
                if (finished) return
                finished = true
                runCatching { nsd.stopServiceDiscovery(listener) }
                runCatching { multicastLock?.release() }
                Log.d(TAG, "NSD finish: $reason urls=${found.size}")
                if (cont.isActive) cont.resume(found.toList())
            }

            listener = object : NsdManager.DiscoveryListener {
                override fun onDiscoveryStarted(serviceType: String) {
                    Log.d(TAG, "NSD started $serviceType")
                }

                override fun onServiceFound(serviceInfo: NsdServiceInfo) {
                    if (!serviceInfo.serviceType.equals(SERVICE_TYPE, ignoreCase = true)) return
                    Log.d(TAG, "NSD service found ${serviceInfo.serviceName}")
                    runCatching {
                        nsd.resolveService(serviceInfo, object : NsdManager.ResolveListener {
                            override fun onResolveFailed(serviceInfo: NsdServiceInfo, errorCode: Int) {
                                Log.e(TAG, "NSD resolve failed $errorCode ${serviceInfo.serviceName}")
                            }

                            override fun onServiceResolved(serviceInfo: NsdServiceInfo) {
                                val host = serviceInfo.host?.hostAddress.orEmpty()
                                val port = serviceInfo.port.takeIf { it > 0 } ?: fallbackPort
                                if (host.isNotBlank()) {
                                    val url = "http://$host:$port"
                                    synchronized(found) { found.add(url) }
                                    Log.d(TAG, "NSD resolved $url")
                                }
                            }
                        })
                    }.onFailure {
                        Log.e(TAG, "NSD resolve exception", it)
                    }
                }

                override fun onServiceLost(serviceInfo: NsdServiceInfo) {
                    Log.d(TAG, "NSD service lost ${serviceInfo.serviceName}")
                }

                override fun onDiscoveryStopped(serviceType: String) {
                    finish("stopped")
                }

                override fun onStartDiscoveryFailed(serviceType: String, errorCode: Int) {
                    Log.e(TAG, "NSD start failed $errorCode")
                    finish("start_failed_$errorCode")
                }

                override fun onStopDiscoveryFailed(serviceType: String, errorCode: Int) {
                    Log.e(TAG, "NSD stop failed $errorCode")
                    finish("stop_failed_$errorCode")
                }
            }

            cont.invokeOnCancellation {
                finish("cancelled")
            }
            handler.postDelayed({ finish("timeout") }, timeoutMs)
            runCatching {
                nsd.discoverServices(SERVICE_TYPE, NsdManager.PROTOCOL_DNS_SD, listener)
            }.onFailure {
                Log.e(TAG, "NSD discover exception", it)
                finish(it.javaClass.simpleName)
            }
        }
}
