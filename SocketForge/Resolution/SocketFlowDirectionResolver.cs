using System.Net;
using SocketForge.Models;

namespace SocketForge.Resolution;

/// <summary>
/// Resolves the logical direction of network flows (Listening, Inbound, Outbound)
/// from the socket state, the index of local listening ports and ephemeral port heuristics.
/// </summary>
public static class SocketFlowDirectionResolver
{
    /// <summary>
    /// Default Linux ephemeral port threshold (/proc/sys/net/ipv4/ip_local_port_range: 32768).
    /// </summary>
    public const int EphemeralPortThreshold = 32768;

    /// <summary>
    /// Standard threshold of privileged system ports (below 1024).
    /// </summary>
    public const int PrivilegedPortThreshold = 1024;

    /// <summary>
    /// Determines the logical direction of a network flow from its endpoints and the known listening ports.
    /// </summary>
    /// <param name="local">Local endpoint.</param>
    /// <param name="remote">Remote endpoint.</param>
    /// <param name="isListeningState">Whether the protocol state is a passive listen (Listen / 0A / State 2).</param>
    /// <param name="listeningPorts">Optional set of local ports currently listening on the machine.</param>
    /// <returns>The logical direction of the flow.</returns>
    public static SocketFlowDirection Resolve(
        SocketEndpoint local,
        SocketEndpoint remote,
        bool isListeningState,
        IReadOnlySet<int>? listeningPorts = null)
    {
        if (isListeningState || remote.Address.Equals(IPAddress.Any) || remote.Address.Equals(IPAddress.IPv6Any) || remote.Port == 0)
        {
            return SocketFlowDirection.Listening;
        }

        // 1. A local port that is an active listening port of this machine makes the flow inbound
        if (listeningPorts is not null && listeningPorts.Contains(local.Port))
        {
            return SocketFlowDirection.Inbound;
        }

        // 2. When the remote port is a port listening on this machine (loopback or intra-host flow)
        // and the local port is not listening, this side is the local client that opened the connection
        if (listeningPorts is not null && listeningPorts.Contains(remote.Port))
        {
            return SocketFlowDirection.Outbound;
        }

        // 3. Heuristic on the dynamic ephemeral port ranges (Linux >= 32768, IANA >= 49152)
        if (local.Port < EphemeralPortThreshold && remote.Port >= EphemeralPortThreshold)
        {
            return SocketFlowDirection.Inbound;
        }

        if (local.Port >= EphemeralPortThreshold && remote.Port < EphemeralPortThreshold)
        {
            return SocketFlowDirection.Outbound;
        }

        // 4. Heuristic on the privileged system ports (below 1024)
        if (local.Port < PrivilegedPortThreshold && remote.Port >= PrivilegedPortThreshold)
        {
            return SocketFlowDirection.Inbound;
        }

        if (local.Port >= PrivilegedPortThreshold && remote.Port < PrivilegedPortThreshold)
        {
            return SocketFlowDirection.Outbound;
        }

        // 5. Default for established flows that no service port identifies
        return SocketFlowDirection.Outbound;
    }
}
