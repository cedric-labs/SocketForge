using System.Net;
using AwesomeAssertions;
using ProcessForge.Contracts;
using ProcessForge.Models;
using SocketForge.Factories;
using SocketForge.Linux;
using SocketForge.Models;
using SocketForge.Windows;
using Xunit;

namespace SocketForge.Tests;

public sealed class SocketForgeTests
{
    private sealed class StubProcessInspector : IProcessInspector
    {
        public Task<ProcessMetadata> ResolveProcessAsync(int processId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProcessMetadata(processId, "stub", "/bin/stub", "", "", "user"));
        }
    }

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

    [Fact]
    public void ComputeLogicalKey_WhenListeningOrUdpFlow_ComputesExpectedKeys()
    {
        // Arrange
        var proc = new ProcessMetadata(1234, "nginx", "/usr/sbin/nginx", "", "", "www-data");
        var listeningConn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Any, 80),
            new SocketEndpoint(IPAddress.Any, 0),
            "Listen",
            proc);

        var udpConn = new ActiveSocketConnection(
            SocketTransportProtocol.Udp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Any, 53),
            new SocketEndpoint(IPAddress.Any, 0),
            "Unknown",
            proc);

        // Act & Assert
        listeningConn.ComputeLogicalKey().Should().Contain("Listen");
        udpConn.ComputeLogicalKey().Should().Contain("53");
    }

    [Fact]
    public void SocketEndpoint_FormattingAndEquality_WorkCorrectly()
    {
        // Arrange
        var ep1 = new SocketEndpoint(IPAddress.Loopback, 8080);
        var ep2 = new SocketEndpoint(IPAddress.Loopback, 8080);
        var ep3 = new SocketEndpoint(IPAddress.Any, 8080);

        // Act & Assert
        ep1.ToString().Should().Be("127.0.0.1:8080");
        (ep1 == ep2).Should().BeTrue();
        (ep1 != ep3).Should().BeTrue();
        ep1.Equals((object)ep2).Should().BeTrue();
        ep1.GetHashCode().Should().Be(ep2.GetHashCode());
    }

    [Fact]
    public void SocketScannerFactory_WhenCalled_ReturnsNonNullScanner()
    {
        // Arrange
        var inspector = new StubProcessInspector();

        // Act
        var scanner = SocketScannerFactory.Create(inspector);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void SocketScannerFactory_WhenInspectorIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => SocketScannerFactory.Create(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructors_WhenInspectorIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var actLinux = () => new LinuxProcSocketScanner(null!);
        actLinux.Should().Throw<ArgumentNullException>();

        var actWindows = () => new WindowsSocketScanner(null!);
        actWindows.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WindowsSocketScanner_DetermineDirectionAndState_ResolvesCorrectly()
    {
        // Arrange
        var localClient = new SocketEndpoint(IPAddress.Loopback, 50000);
        var remoteServer = new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443);
        var localServer = new SocketEndpoint(IPAddress.Loopback, 80);
        var remoteClient = new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 50000);

        // Act & Assert
        WindowsSocketScanner.DetermineDirection(localClient, new SocketEndpoint(IPAddress.Any, 0), 2)
            .Should().Be(SocketFlowDirection.Listening);
        WindowsSocketScanner.DetermineDirection(localClient, remoteServer, 5)
            .Should().Be(SocketFlowDirection.Outbound);
        WindowsSocketScanner.DetermineDirection(localServer, remoteClient, 5)
            .Should().Be(SocketFlowDirection.Inbound);

        WindowsSocketScanner.MapTcpState(1).Should().Be("Closed");
        WindowsSocketScanner.MapTcpState(2).Should().Be("Listen");
        WindowsSocketScanner.MapTcpState(3).Should().Be("SynSent");
        WindowsSocketScanner.MapTcpState(4).Should().Be("SynReceived");
        WindowsSocketScanner.MapTcpState(5).Should().Be("Established");
        WindowsSocketScanner.MapTcpState(6).Should().Be("FinWait1");
        WindowsSocketScanner.MapTcpState(7).Should().Be("FinWait2");
        WindowsSocketScanner.MapTcpState(8).Should().Be("CloseWait");
        WindowsSocketScanner.MapTcpState(9).Should().Be("Closing");
        WindowsSocketScanner.MapTcpState(10).Should().Be("LastAck");
        WindowsSocketScanner.MapTcpState(11).Should().Be("TimeWait");
        WindowsSocketScanner.MapTcpState(12).Should().Be("DeleteTCB");
        WindowsSocketScanner.MapTcpState(99).Should().Be("Unknown(99)");
    }

    [Fact]
    public async Task WindowsSocketScanner_WhenExecutedOnWindows_ReturnsConnections()
    {
        // Arrange
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var inspector = new StubProcessInspector();
        var scanner = new WindowsSocketScanner(inspector);

        // Act
        var results = await scanner.ScanSocketsAsync(CancellationToken.None);

        // Assert
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task LinuxProcSocketScanner_WhenDirectoryDoesNotExist_ReturnsEmpty()
    {
        // Arrange
        var inspector = new StubProcessInspector();
        var scanner = new LinuxProcSocketScanner(inspector, @"C:\non_existent_proc_dir_" + Guid.NewGuid().ToString("N"));

        // Act
        var results = await scanner.ScanSocketsAsync(CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task LinuxProcSocketScanner_WhenMockProcNetExists_ParsesConnections()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "socketforge_test_" + Guid.NewGuid().ToString("N"));
        var netDir = Path.Combine(tempDir, "net");
        Directory.CreateDirectory(netDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "1234", "fd"));
        Directory.CreateDirectory(Path.Combine(tempDir, "5678"));
        Directory.CreateDirectory(Path.Combine(tempDir, "non_numeric_pid"));

        await File.WriteAllTextAsync(Path.Combine(tempDir, "1234", "fd", "1"), "dummy");

        try
        {
            var tcpContent = "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode\n" +
                             "   0: 0100007F:0050 00000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 99999 1 0000000000000000 100 0 0 10 0\n" +
                             "   1: 0100007F:C001 0100007F:0050 01 00000000:00000000 00:00000000 00000000  1000        0 99998 1 0000000000000000 100 0 0 10 0\n" +
                             "   2: 0100007F:C002 0100007F:0050 02 00000000:00000000 00:00000000 00000000  1000        0 99997 1 0000000000000000 100 0 0 10 0\n" +
                             "   3: 0100007F:C003 0100007F:0050 03 00000000:00000000 00:00000000 00000000  1000        0 99996 1 0000000000000000 100 0 0 10 0\n" +
                             "   4: 0100007F:C004 0100007F:0050 04 00000000:00000000 00:00000000 00000000  1000        0 99995 1 0000000000000000 100 0 0 10 0\n" +
                             "   5: 0100007F:C005 0100007F:0050 05 00000000:00000000 00:00000000 00000000  1000        0 99994 1 0000000000000000 100 0 0 10 0\n" +
                             "   6: 0100007F:C006 0100007F:0050 06 00000000:00000000 00:00000000 00000000  1000        0 99993 1 0000000000000000 100 0 0 10 0\n" +
                             "   7: 0100007F:C007 0100007F:0050 07 00000000:00000000 00:00000000 00000000  1000        0 99992 1 0000000000000000 100 0 0 10 0\n" +
                             "   8: 0100007F:C008 0100007F:0050 08 00000000:00000000 00:00000000 00000000  1000        0 99991 1 0000000000000000 100 0 0 10 0\n" +
                             "   9: 0100007F:C009 0100007F:0050 09 00000000:00000000 00:00000000 00000000  1000        0 99990 1 0000000000000000 100 0 0 10 0\n" +
                             "  10: 0100007F:C00A 0100007F:0050 FF 00000000:00000000 00:00000000 00000000  1000        0 99989 1 0000000000000000 100 0 0 10 0\n" +
                             "  11: invalid_line\n";
            await File.WriteAllTextAsync(Path.Combine(netDir, "tcp"), tcpContent);

            var tcp6Content = "  sl  local_address                         remote_address                        st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode\n" +
                              "   0: 0000000000000000000000000100007F:1F90 00000000000000000000000000000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 99988 1 0000000000000000 100 0 0 10 0\n" +
                              "   1: bad_endpoint:0050 00000000000000000000000000000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 99987 1 0000000000000000 100 0 0 10 0\n" +
                              "   2: invalid_no_colon 00000000000000000000000000000000:0000 0A 00000000:00000000 00:00000000 00000000  1000        0 99986 1 0000000000000000 100 0 0 10 0\n";
            await File.WriteAllTextAsync(Path.Combine(netDir, "tcp6"), tcp6Content);

            var udpContent = "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode\n" +
                             "   0: 00000000:0035 00000000:0000 07 00000000:00000000 00:00000000 00000000     0        0 88888 1 0000000000000000 100 0 0 10 0\n";
            await File.WriteAllTextAsync(Path.Combine(netDir, "udp"), udpContent);

            var udp6Content = "  sl  local_address                         remote_address                        st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode\n" +
                              "   0: 00000000000000000000000000000000:007B 00000000000000000000000000000000:0000 07 00000000:00000000 00:00000000 00000000     0        0 88887 1 0000000000000000 100 0 0 10 0\n";
            await File.WriteAllTextAsync(Path.Combine(netDir, "udp6"), udp6Content);

            var inspector = new StubProcessInspector();
            var scanner = new LinuxProcSocketScanner(inspector, tempDir);

            // Act
            var connections = await scanner.ScanSocketsAsync(CancellationToken.None);

            // Assert
            connections.Should().HaveCount(16);
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.LocalEndpoint.Port == 80 && c.Direction == SocketFlowDirection.Listening);
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.RemoteEndpoint.Port == 80 && c.Direction == SocketFlowDirection.Outbound);
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.State == "SynRecv");
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.State == "FinWait1");
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.State == "FinWait2");
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.State == "TimeWait");
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.State == "Close");
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.State == "LastAck");
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.State == "State(FF)");
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Tcp && c.LocalEndpoint.Port == 8080);
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Udp && c.LocalEndpoint.Port == 53);
            connections.Should().Contain(c => c.Protocol == SocketTransportProtocol.Udp && c.LocalEndpoint.Port == 123);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LinuxProcSocketScanner_WhenInboundConnectionReceivedOnListeningPort_ResolvesDirectionAsInbound()
    {
        // Arrange: simulated SSH server (port 22) receiving an inbound connection from an ephemeral client port (52205 / 0xCBED)
        var tempDir = Path.Combine(Path.GetTempPath(), "socketforge_ssh_test_" + Guid.NewGuid().ToString("N"));
        var netDir = Path.Combine(tempDir, "net");
        Directory.CreateDirectory(netDir);

        try
        {
            var tcpContent = "  sl  local_address rem_address   st tx_queue rx_queue tr tm->when retrnsmt   uid  timeout inode\n" +
                             "   0: 00000000:0016 00000000:0000 0A 00000000:00000000 00:00000000 00000000     0        0 11111 1 0000000000000000 100 0 0 10 0\n" +
                             "   1: 2A01A8C0:0016 1401A8C0:CBED 01 00000000:00000000 00:00000000 00000000  1000        0 11112 1 0000000000000000 100 0 0 10 0\n";
            await File.WriteAllTextAsync(Path.Combine(netDir, "tcp"), tcpContent);

            var inspector = new StubProcessInspector();
            var scanner = new LinuxProcSocketScanner(inspector, tempDir);

            // Act
            var connections = await scanner.ScanSocketsAsync(CancellationToken.None);

            // Assert
            connections.Should().HaveCount(2);

            var listener = connections.Single(c => c.State == "Listen");
            listener.LocalEndpoint.Port.Should().Be(22);
            listener.Direction.Should().Be(SocketFlowDirection.Listening);

            var inboundConn = connections.Single(c => c.State == "Established");
            inboundConn.LocalEndpoint.Port.Should().Be(22);
            inboundConn.RemoteEndpoint.Port.Should().Be(52205);
            inboundConn.Direction.Should().Be(SocketFlowDirection.Inbound);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
