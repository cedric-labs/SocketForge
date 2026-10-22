using System.Globalization;
using System.Net;
using ProcessForge.Contracts;
using SocketForge.Contracts;
using SocketForge.Models;

namespace SocketForge.Linux;

/// <summary>
/// Scanneur de sockets Linux basé sur l'analyse de /proc/net.
/// </summary>
public sealed class LinuxProcSocketScanner(IProcessInspector processInspector, string procRoot = "/proc") : ISocketScanner
{
    private readonly IProcessInspector _processInspector = processInspector ?? throw new ArgumentNullException(nameof(processInspector));
    private readonly string _procRoot = string.IsNullOrWhiteSpace(procRoot) ? "/proc" : procRoot;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveSocketConnection>> ScanSocketsAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<ActiveSocketConnection>();
        var inodeToPid = BuildInodeToPidMap();

        var tcpPath = Path.Combine(_procRoot, "net", "tcp");
        if (File.Exists(tcpPath))
        {
            var tcpLines = await File.ReadAllLinesAsync(tcpPath, cancellationToken).ConfigureAwait(false);
            await ParseProcNetLinesAsync(tcpLines, SocketTransportProtocol.Tcp, inodeToPid, records, cancellationToken).ConfigureAwait(false);
        }

        var tcp6Path = Path.Combine(_procRoot, "net", "tcp6");
        if (File.Exists(tcp6Path))
        {
            var tcp6Lines = await File.ReadAllLinesAsync(tcp6Path, cancellationToken).ConfigureAwait(false);
            await ParseProcNetLinesAsync(tcp6Lines, SocketTransportProtocol.Tcp, inodeToPid, records, cancellationToken).ConfigureAwait(false);
        }

        var udpPath = Path.Combine(_procRoot, "net", "udp");
        if (File.Exists(udpPath))
        {
            var udpLines = await File.ReadAllLinesAsync(udpPath, cancellationToken).ConfigureAwait(false);
            await ParseProcNetLinesAsync(udpLines, SocketTransportProtocol.Udp, inodeToPid, records, cancellationToken).ConfigureAwait(false);
        }

        var udp6Path = Path.Combine(_procRoot, "net", "udp6");
        if (File.Exists(udp6Path))
        {
            var udp6Lines = await File.ReadAllLinesAsync(udp6Path, cancellationToken).ConfigureAwait(false);
            await ParseProcNetLinesAsync(udp6Lines, SocketTransportProtocol.Udp, inodeToPid, records, cancellationToken).ConfigureAwait(false);
        }

        return records;
    }

    private async Task ParseProcNetLinesAsync(
        string[] lines,
        SocketTransportProtocol protocol,
        Dictionary<string, int> inodeToPid,
        List<ActiveSocketConnection> records,
        CancellationToken cancellationToken)
    {
        for (var i = 1; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 10)
            {
                continue;
            }

            var localEp = ParseEndpoint(parts[1]);
            var remoteEp = ParseEndpoint(parts[2]);
            var stateHex = parts[3];
            var inode = parts[9];

            _ = inodeToPid.TryGetValue(inode, out var pid);
            var process = await _processInspector.ResolveProcessAsync(pid, cancellationToken).ConfigureAwait(false);

            var direction = remoteEp.Port == 0 || remoteEp.Address.Equals(IPAddress.Any) || remoteEp.Address.Equals(IPAddress.IPv6Any)
                ? SocketFlowDirection.Listening
                : SocketFlowDirection.Outbound;

            records.Add(new ActiveSocketConnection(
                Protocol: protocol,
                Direction: direction,
                LocalEndpoint: localEp,
                RemoteEndpoint: remoteEp,
                State: MapLinuxState(stateHex),
                Process: process));
        }
    }

    private static SocketEndpoint ParseEndpoint(string hexEndpoint)
    {
        var tokens = hexEndpoint.Split(':');
        if (tokens.Length != 2)
        {
            return new SocketEndpoint(IPAddress.Any, 0);
        }

        var ipHex = tokens[0];
        var port = int.Parse(tokens[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        if (ipHex.Length == 8)
        {
            var ipInt = uint.Parse(ipHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new SocketEndpoint(new IPAddress(ipInt), port);
        }

        if (ipHex.Length == 32)
        {
            var bytes = new byte[16];
            for (var i = 0; i < 4; i++)
            {
                var chunk = uint.Parse(ipHex.Substring(i * 8, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var chunkBytes = BitConverter.GetBytes(chunk);
                Array.Copy(chunkBytes, 0, bytes, i * 4, 4);
            }

            return new SocketEndpoint(new IPAddress(bytes), port);
        }

        return new SocketEndpoint(IPAddress.Any, port);
    }

    private static string MapLinuxState(string stateHex)
    {
        return stateHex switch
        {
            "0A" => "Listen",
            "01" => "Established",
            "02" => "SynSent",
            "03" => "SynRecv",
            "04" => "FinWait1",
            "05" => "FinWait2",
            "06" => "TimeWait",
            "07" => "Close",
            "08" => "CloseWait",
            "09" => "LastAck",
            _ => $"State({stateHex})"
        };
    }

    private Dictionary<string, int> BuildInodeToPidMap()
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!Directory.Exists(_procRoot))
        {
            return map;
        }

        try
        {
            var procDirs = Directory.GetDirectories(_procRoot);
            foreach (var dir in procDirs)
            {
                var dirName = Path.GetFileName(dir);
                if (!int.TryParse(dirName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
                {
                    continue;
                }

                var fdDir = Path.Combine(dir, "fd");
                if (!Directory.Exists(fdDir))
                {
                    continue;
                }

                try
                {
                    var links = Directory.GetFiles(fdDir);
                    foreach (var link in links)
                    {
                        var linkTarget = new FileInfo(link).LinkTarget ?? File.ResolveLinkTarget(link, returnFinalTarget: false)?.FullName;
                        if (linkTarget is not null && linkTarget.Contains("socket:[", StringComparison.Ordinal))
                        {
                            var startIdx = linkTarget.IndexOf("socket:[", StringComparison.Ordinal) + "socket:[".Length;
                            var endIdx = linkTarget.IndexOf(']', startIdx);
                            if (endIdx > startIdx)
                            {
                                var inode = linkTarget[startIdx..endIdx];
                                map[inode] = pid;
                            }
                        }
                    }
                }
                catch (IOException)
                {
                    // Erreur lecture I/O
                }
                catch (UnauthorizedAccessException)
                {
                    // Droits insuffisants
                }
            }
        }
        catch (IOException)
        {
            // Erreur globale /proc
        }

        return map;
    }
}
