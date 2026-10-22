using ProcessForge.Models;

namespace SocketForge.Models;

/// <summary>
/// Représentation immuable d'une socket réseau active et de son processus propriétaire.
/// </summary>
/// <param name="Protocol">Protocole de transport.</param>
/// <param name="Direction">Direction déterminée du flux.</param>
/// <param name="LocalEndpoint">Point de terminaison local.</param>
/// <param name="RemoteEndpoint">Point de terminaison distant.</param>
/// <param name="State">État textuel de la socket (ex: Established, Listen, etc.).</param>
/// <param name="Process">Métadonnées du processus propriétaire résolu via ProcessForge.</param>
public sealed record ActiveSocketConnection(
    SocketTransportProtocol Protocol,
    SocketFlowDirection Direction,
    SocketEndpoint LocalEndpoint,
    SocketEndpoint RemoteEndpoint,
    string State,
    ProcessMetadata Process)
{
    /// <summary>
    /// Calcule la clé canonique de dédoublonnage logique de cette socket.
    /// </summary>
    public string ComputeLogicalKey()
    {
        var targetOrPath = string.IsNullOrWhiteSpace(Process.TargetExecutable)
            ? Process.ExecutablePath
            : Process.TargetExecutable;

        return Direction switch
        {
            SocketFlowDirection.Outbound => $"{Protocol}:{Direction}:{targetOrPath}:{RemoteEndpoint.Address}:{RemoteEndpoint.Port}",
            SocketFlowDirection.Inbound => $"{Protocol}:{Direction}:{targetOrPath}:{LocalEndpoint.Port}:{RemoteEndpoint.Address}",
            SocketFlowDirection.Listening => $"{Protocol}:{Direction}:{targetOrPath}:{LocalEndpoint.Port}",
            _ => $"{Protocol}:{Direction}:{targetOrPath}:{LocalEndpoint}:{RemoteEndpoint}"
        };
    }
}
