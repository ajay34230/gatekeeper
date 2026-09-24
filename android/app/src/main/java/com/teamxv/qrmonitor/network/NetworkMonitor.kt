package com.teamxv.qrmonitor.network

import android.content.Context
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import java.net.Inet4Address

class NetworkMonitor(private val context: Context) {
    fun current(): Pair<String, String> {
        val cm = context.getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
        val network = cm.activeNetwork ?: return "Disconnected" to ""
        val caps = cm.getNetworkCapabilities(network) ?: return "Disconnected" to ""
        if (!caps.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)) {
            // A local-only Wi-Fi/LAN still has a network object, but Android may not
            // advertise Internet capability. Preserve the transport information so
            // local-server testing remains possible.
            val transport = transport(caps)
            return transport to ipv4(cm, network)
        }
        return transport(caps) to ipv4(cm, network)
    }

    private fun transport(caps: NetworkCapabilities): String = when {
        caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) -> "Wi-Fi"
        caps.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> "Ethernet"
        caps.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> "Mobile Data"
        else -> "Network"
    }

    private fun ipv4(cm: ConnectivityManager, network: android.net.Network): String =
        cm.getLinkProperties(network)?.linkAddresses
            ?.asSequence()
            ?.map { it.address }
            ?.filterIsInstance<Inet4Address>()
            ?.filterNot { it.isLoopbackAddress }
            ?.firstOrNull()
            ?.hostAddress
            .orEmpty()

    fun isNetworkAvailable(): Boolean = current().first != "Disconnected"
}
