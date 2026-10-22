using System.Net;

namespace SocketForge.Models;

/// <summary>
/// Représente le protocole de transport réseau.
/// </summary>
public enum SocketTransportProtocol
{
    /// <summary>
    /// Protocole TCP (Transmission Control Protocol).
    /// </summary>
    Tcp,

    /// <summary>
    /// Protocole UDP (User Datagram Protocol).
    /// </summary>
    Udp
}

/// <summary>
/// Direction logique d'un flux par rapport au point d'observation local.
/// </summary>
public enum SocketFlowDirection
{
    /// <summary>
    /// Direction indéterminée.
    /// </summary>
    Unknown,

    /// <summary>
    /// Connexion entrante vers un service local.
    /// </summary>
    Inbound,

    /// <summary>
    /// Connexion sortante initiée localement vers l'extérieur.
    /// </summary>
    Outbound,

    /// <summary>
    /// Socket en écoute locale (Listen).
    /// </summary>
    Listening
}

/// <summary>
/// Point de terminaison réseau typé (IP et Port).
/// </summary>
public readonly record struct SocketEndpoint
{
    /// <summary>
    /// Initialise un nouveau point de terminaison réseau.
    /// </summary>
    public SocketEndpoint(IPAddress address, int port)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Le port réseau doit être compris entre 0 et 65535.");
        }

        Address = address;
        Port = port;
    }

    /// <summary>
    /// Adresse IP.
    /// </summary>
    public IPAddress Address { get; }

    /// <summary>
    /// Numéro de port.
    /// </summary>
    public int Port { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{Address}]:{Port}"
            : $"{Address}:{Port}";
    }
}
