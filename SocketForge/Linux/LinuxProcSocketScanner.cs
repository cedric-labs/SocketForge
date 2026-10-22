using System.Globalization;
using System.Net;
using ProcessForge.Contracts;
using SocketForge.Contracts;
using SocketForge.Models;

namespace SocketForge.Linux;

/// <summary>
/// Scanneur de sockets Linux basé sur l'analyse de /proc/net.
/// </summary>
public sealed class LinuxProcSocketScanner(IProcessInspector processInspector) : ISocketScanner
{
    private readonly IProcessInspector _processInspector = processInspector ?? throw new ArgumentNullException(nameof(processInspector));

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveSocketConnection>> ScanSocketsAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<ActiveSocketConnection>();
        var inodeToPid = BuildInodeToPidMap();

        if (File.Exists("/proc/net/tcp"))
        {
            var tcpLines = await File.ReadAllLinesAsync("/proc/net/tcp", cancellationToken).ConfigureAwait(false);
            ParseProcNetLines(tcpLines, SocketTransportProtocol.Tcp, inodeToPid, records, cancellationToken);
        }

        if (File.Exists("/proc/net/udp"))
        {
            var udpLines = await File.ReadAllLinesAsync("/proc/net/udp", cancellationToken).ConfigureAwait(false);
            ParseProcNetLines(udpLines, SocketTransportProtocol.Udp, inodeToPid, records, cancellationToken);
        }

        return records;
    }

    private void ParseProcNetLines(
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
            var process = _processInspector.ResolveProcessAsync(pid, cancellationToken).GetAwaiter().GetResult();

            var direction = remoteEp.Port == 0 || remoteEp.Address.Equals(IPAddress.Any)
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

        var ipInt = uint.Parse(tokens[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var port = int.Parse(tokens[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        return new SocketEndpoint(new IPAddress(ipInt), port);
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

    private static Dictionary<string, int> BuildInodeToPidMap()
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        if (!Directory.Exists("/proc"))
        {
            return map;
        }

        try
        {
            var procDirs = Directory.GetDirectories("/proc");
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
                        var target = File.ResolveLinkTarget(link, returnFinalTarget: false);
                        if (target is not null && target.FullName.StartsWith("socket:[", StringComparison.Ordinal))
                        {
                            var inode = target.FullName.Replace("socket:[", "", StringComparison.Ordinal).Replace("]", "", StringComparison.Ordinal);
                            map[inode] = pid;
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
