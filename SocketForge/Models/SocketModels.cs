using System.Net;

namespace SocketForge.Models;

/// <summary>
/// Network transport protocol.
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
/// Logical direction of a flow, seen from the local host.
/// </summary>
public enum SocketFlowDirection
{
    /// <summary>
    /// Unknown direction.
    /// </summary>
    Unknown,

    /// <summary>
    /// Inbound connection to a local service.
    /// </summary>
    Inbound,

    /// <summary>
    /// Outbound connection opened locally.
    /// </summary>
    Outbound,

    /// <summary>
    /// Local listening socket (Listen).
    /// </summary>
    Listening
}

/// <summary>
/// Typed network endpoint (IP address and port).
/// </summary>
public readonly record struct SocketEndpoint
{
    /// <summary>
    /// Creates a network endpoint.
    /// </summary>
    public SocketEndpoint(IPAddress address, int port)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "The network port must be between 0 and 65535.");
        }

        Address = address;
        Port = port;
    }

    /// <summary>
    /// Adresse IP.
    /// </summary>
    public IPAddress Address { get; }

    /// <summary>
    /// Port number.
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
