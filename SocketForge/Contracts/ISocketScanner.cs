using SocketForge.Models;

namespace SocketForge.Contracts;

/// <summary>
/// Contrat d'énumération des connexions et sockets réseau du système hôte sans dépendance Npcap.
/// </summary>
public interface ISocketScanner
{
    /// <summary>
    /// Scanne l'intégralité des connexions TCP et sockets UDP actives.
    /// </summary>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Collection des connexions réseau observées.</returns>
    Task<IReadOnlyList<ActiveSocketConnection>> ScanSocketsAsync(CancellationToken cancellationToken = default);
}
