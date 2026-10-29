using ProcessForge.Models;

namespace SocketForge.Models;

/// <summary>
/// Immutable view of an active network socket and its owning process.
/// </summary>
/// <param name="Protocol">Transport protocol.</param>
/// <param name="Direction">Resolved flow direction.</param>
/// <param name="LocalEndpoint">Local endpoint.</param>
/// <param name="RemoteEndpoint">Remote endpoint.</param>
/// <param name="State">Socket state as text (for example Established or Listen).</param>
/// <param name="Process">Metadata of the owning process, resolved through ProcessForge.</param>
public sealed record ActiveSocketConnection(
    SocketTransportProtocol Protocol,
    SocketFlowDirection Direction,
    SocketEndpoint LocalEndpoint,
    SocketEndpoint RemoteEndpoint,
    string State,
    ProcessMetadata Process)
{
    /// <summary>
    /// Computes the canonical key used to deduplicate this socket.
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
