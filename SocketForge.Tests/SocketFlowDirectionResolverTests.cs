using System.Net;
using FluentAssertions;
using SocketForge.Models;
using SocketForge.Resolution;
using Xunit;

namespace SocketForge.Tests;

public sealed class SocketFlowDirectionResolverTests
{
    [Theory]
    [InlineData("0.0.0.0", 0, true)]
    [InlineData("::", 0, true)]
    [InlineData("192.168.1.1", 0, true)]
    [InlineData("0.0.0.0", 8080, true)]
    public void Resolve_WhenListeningStateOrZeroRemotePort_ReturnsListening(string remoteIp, int remotePort, bool isListeningState)
    {
        // Arrange
        var local = new SocketEndpoint(IPAddress.Any, 80);
        var remote = new SocketEndpoint(IPAddress.Parse(remoteIp), remotePort);

        // Act
        var result = SocketFlowDirectionResolver.Resolve(local, remote, isListeningState);

        // Assert
        result.Should().Be(SocketFlowDirection.Listening);
    }

    [Fact]
    public void Resolve_WhenLocalPortIsInListeningPorts_ReturnsInbound()
    {
        // Arrange : Service local sur port 8080 (non privilégié mais en écoute), client distant sur port éphémère 54321
        var local = new SocketEndpoint(IPAddress.Parse("192.168.1.100"), 8080);
        var remote = new SocketEndpoint(IPAddress.Parse("192.168.1.50"), 54321);
        var listeningPorts = new HashSet<int> { 80, 443, 8080 };

        // Act
        var result = SocketFlowDirectionResolver.Resolve(local, remote, isListeningState: false, listeningPorts);

        // Assert
        result.Should().Be(SocketFlowDirection.Inbound);
    }

    [Fact]
    public void Resolve_WhenSshConnectionReceivedOnPort22_ReturnsInbound()
    {
        // Arrange : Cas PopOs SSH (port 22) recevant une connexion depuis Windows 11 (port éphémère 52145)
        var local = new SocketEndpoint(IPAddress.Parse("192.168.1.42"), 22);
        var remote = new SocketEndpoint(IPAddress.Parse("192.168.1.20"), 52145);
        var listeningPorts = new HashSet<int> { 22 };

        // Act
        var resultWithListeners = SocketFlowDirectionResolver.Resolve(local, remote, isListeningState: false, listeningPorts);
        var resultFallbackHeuristic = SocketFlowDirectionResolver.Resolve(local, remote, isListeningState: false, listeningPorts: null);

        // Assert
        resultWithListeners.Should().Be(SocketFlowDirection.Inbound);
        resultFallbackHeuristic.Should().Be(SocketFlowDirection.Inbound);
    }

    [Fact]
    public void Resolve_WhenRemotePortIsInListeningPortsAndLocalIsNot_ReturnsOutbound()
    {
        // Arrange : Client local sur port éphémère 49200 se connectant à un service local 8080
        var local = new SocketEndpoint(IPAddress.Parse("127.0.0.1"), 49200);
        var remote = new SocketEndpoint(IPAddress.Parse("127.0.0.1"), 8080);
        var listeningPorts = new HashSet<int> { 8080 };

        // Act
        var result = SocketFlowDirectionResolver.Resolve(local, remote, isListeningState: false, listeningPorts);

        // Assert
        result.Should().Be(SocketFlowDirection.Outbound);
    }

    [Fact]
    public void Resolve_WhenLocalPortIsPrivilegedAndRemoteIsEphemeralWithoutKnownListeners_ReturnsInbound()
    {
        // Arrange : Port local 443, port distant 51234, sans liste d'écoute fournie
        var local = new SocketEndpoint(IPAddress.Parse("10.0.0.1"), 443);
        var remote = new SocketEndpoint(IPAddress.Parse("10.0.0.2"), 51234);

        // Act
        var result = SocketFlowDirectionResolver.Resolve(local, remote, isListeningState: false);

        // Assert
        result.Should().Be(SocketFlowDirection.Inbound);
    }

    [Fact]
    public void Resolve_WhenLocalPortIsEphemeralAndRemoteIsPrivileged_ReturnsOutbound()
    {
        // Arrange : Client local (port 52000) appelant un serveur distant (port 443)
        var local = new SocketEndpoint(IPAddress.Parse("10.0.0.1"), 52000);
        var remote = new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443);

        // Act
        var result = SocketFlowDirectionResolver.Resolve(local, remote, isListeningState: false);

        // Assert
        result.Should().Be(SocketFlowDirection.Outbound);
    }

    [Fact]
    public void Resolve_WhenBothPortsAreEphemeralAndLocalNotInListening_ReturnsOutbound()
    {
        // Arrange : Deux ports éphémères (ex: P2P ou proxy dynamique)
        var local = new SocketEndpoint(IPAddress.Parse("10.0.0.1"), 50000);
        var remote = new SocketEndpoint(IPAddress.Parse("10.0.0.2"), 50001);

        // Act
        var result = SocketFlowDirectionResolver.Resolve(local, remote, isListeningState: false);

        // Assert
        result.Should().Be(SocketFlowDirection.Outbound);
    }
}
