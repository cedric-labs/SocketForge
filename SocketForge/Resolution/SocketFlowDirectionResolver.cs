using System.Net;
using SocketForge.Models;

namespace SocketForge.Resolution;

/// <summary>
/// Évaluateur de la direction logique des flux réseau (Listening, Inbound, Outbound)
/// fondé sur l'état de la socket, l'indexation des ports d'écoute locaux et les heuristiques de ports éphémères.
/// </summary>
public static class SocketFlowDirectionResolver
{
    /// <summary>
    /// Seuil de port éphémère Linux par défaut (/proc/sys/net/ipv4/ip_local_port_range : 32768).
    /// </summary>
    public const int EphemeralPortThreshold = 32768;

    /// <summary>
    /// Seuil standard des ports privilégiés / réservés du système (< 1024).
    /// </summary>
    public const int PrivilegedPortThreshold = 1024;

    /// <summary>
    /// Détermine la direction logique d'un flux réseau à partir des points de terminaison et des ports d'écoute connus.
    /// </summary>
    /// <param name="local">Point de terminaison local.</param>
    /// <param name="remote">Point de terminaison distant.</param>
    /// <param name="isListeningState">Indique si l'état protocolaire correspond à une écoute passive (Listen / 0A / State 2).</param>
    /// <param name="listeningPorts">Ensemble optionnel des ports locaux actifs en écoute sur la machine.</param>
    /// <returns>La direction logique identifiée pour le flux.</returns>
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

        // 1. Si le port local correspond à un port d'écoute actif hébergé par la machine, c'est formellement un flux entrant
        if (listeningPorts is not null && listeningPorts.Contains(local.Port))
        {
            return SocketFlowDirection.Inbound;
        }

        // 2. Si le port distant est un port d'écoute actif hébergé localement (flux loopback/intra-hôte)
        // et que le port local n'écoute pas, il s'agit du client initiateur local
        if (listeningPorts is not null && listeningPorts.Contains(remote.Port))
        {
            return SocketFlowDirection.Outbound;
        }

        // 3. Heuristique sur les plages de ports éphémères dynamiques (Linux >= 32768, IANA >= 49152)
        if (local.Port < EphemeralPortThreshold && remote.Port >= EphemeralPortThreshold)
        {
            return SocketFlowDirection.Inbound;
        }

        if (local.Port >= EphemeralPortThreshold && remote.Port < EphemeralPortThreshold)
        {
            return SocketFlowDirection.Outbound;
        }

        // 4. Heuristique sur les ports système privilégiés (< 1024)
        if (local.Port < PrivilegedPortThreshold && remote.Port >= PrivilegedPortThreshold)
        {
            return SocketFlowDirection.Inbound;
        }

        if (local.Port >= PrivilegedPortThreshold && remote.Port < PrivilegedPortThreshold)
        {
            return SocketFlowDirection.Outbound;
        }

        // 5. Par défaut pour les flux établis non identifiés par les ports de service
        return SocketFlowDirection.Outbound;
    }
}
