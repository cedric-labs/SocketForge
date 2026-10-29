using SocketForge.Models;

namespace SocketForge.Contracts;

/// <summary>
/// Enumerates the network connections and sockets of the host system, without depending on Npcap.
/// </summary>
public interface ISocketScanner
{
    /// <summary>
    /// Scans all active TCP connections and UDP sockets.
    /// </summary>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>The observed network connections.</returns>
    Task<IReadOnlyList<ActiveSocketConnection>> ScanSocketsAsync(CancellationToken cancellationToken = default);
}
