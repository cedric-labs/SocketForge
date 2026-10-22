using System.Net;
using FluentAssertions;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace SocketForge.Tests;

public sealed class SocketForgeTests
{
    [Fact]
    public void ComputeLogicalKey_WhenOutboundFlowWithDifferentEphemeralPort_PreservesIdentity()
    {
        // Arrange
        var proc = new ProcessMetadata(1234, "curl", @"C:\tools\curl.exe", "", "", "User");
        var conn1 = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50001),
            new SocketEndpoint(IPAddress.Parse("8.8.8.8"), 443),
            "Established",
            proc);

        var conn2 = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50002),
            new SocketEndpoint(IPAddress.Parse("8.8.8.8"), 443),
            "Established",
            proc);

        // Act & Assert
        conn1.ComputeLogicalKey().Should().Be(conn2.ComputeLogicalKey());
    }
}
