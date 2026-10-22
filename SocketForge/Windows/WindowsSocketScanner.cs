using System.Net;
using System.Runtime.InteropServices;
using ProcessForge.Contracts;
using SocketForge.Contracts;
using SocketForge.Models;

namespace SocketForge.Windows;

/// <summary>
/// Scanneur de sockets Windows exploitant l'API native IP Helper (iphlpapi.dll).
/// </summary>
public sealed class WindowsSocketScanner(IProcessInspector processInspector) : ISocketScanner
{
    private readonly IProcessInspector _processInspector = processInspector ?? throw new ArgumentNullException(nameof(processInspector));

    private const int AfInet = 2;
    private const int TcpTableOwnerPidAll = 5;
    private const int UdpTableOwnerPid = 1;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int pdwSize,
        bool bOrder,
        int ulAf,
        int tableClass,
        uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable,
        ref int pdwSize,
        bool bOrder,
        int ulAf,
        int tableClass,
        uint reserved);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveSocketConnection>> ScanSocketsAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<ActiveSocketConnection>();

        var tcpRows = ReadNativeTcpTable();
        for (var i = 0; i < tcpRows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = tcpRows[i];
            var process = await _processInspector.ResolveProcessAsync(row.OwningPid, cancellationToken).ConfigureAwait(false);

            var localEp = new SocketEndpoint(new IPAddress(row.LocalAddr), ConvertPort(row.LocalPort));
            var remoteEp = new SocketEndpoint(new IPAddress(row.RemoteAddr), ConvertPort(row.RemotePort));

            var direction = DetermineDirection(localEp, remoteEp, row.State);

            records.Add(new ActiveSocketConnection(
                Protocol: SocketTransportProtocol.Tcp,
                Direction: direction,
                LocalEndpoint: localEp,
                RemoteEndpoint: remoteEp,
                State: MapTcpState(row.State),
                Process: process));
        }

        var udpRows = ReadNativeUdpTable();
        for (var i = 0; i < udpRows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = udpRows[i];
            var process = await _processInspector.ResolveProcessAsync(row.OwningPid, cancellationToken).ConfigureAwait(false);

            var localEp = new SocketEndpoint(new IPAddress(row.LocalAddr), ConvertPort(row.LocalPort));
            var remoteEp = new SocketEndpoint(IPAddress.Any, 0);

            records.Add(new ActiveSocketConnection(
                Protocol: SocketTransportProtocol.Udp,
                Direction: SocketFlowDirection.Listening,
                LocalEndpoint: localEp,
                RemoteEndpoint: remoteEp,
                State: "UDP",
                Process: process));
        }

        return records;
    }

    private static SocketFlowDirection DetermineDirection(SocketEndpoint local, SocketEndpoint remote, uint state)
    {
        if (state == 2 || remote.Address.Equals(IPAddress.Any) || remote.Port == 0)
        {
            return SocketFlowDirection.Listening;
        }

        if (local.Port > 1024 && remote.Port < 1024)
        {
            return SocketFlowDirection.Outbound;
        }

        if (local.Port < 1024 && remote.Port > 1024)
        {
            return SocketFlowDirection.Inbound;
        }

        return SocketFlowDirection.Outbound;
    }

    private static int ConvertPort(uint port)
    {
        return (int)(((port & 0xFF) << 8) | ((port >> 8) & 0xFF));
    }

    private static string MapTcpState(uint state)
    {
        return state switch
        {
            1 => "Closed",
            2 => "Listen",
            3 => "SynSent",
            4 => "SynReceived",
            5 => "Established",
            6 => "FinWait1",
            7 => "FinWait2",
            8 => "CloseWait",
            9 => "Closing",
            10 => "LastAck",
            11 => "TimeWait",
            12 => "DeleteTCB",
            _ => $"Unknown({state})"
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MibTcpRowOwnerPid
    {
        public readonly uint State;
        public readonly uint LocalAddr;
        public readonly uint LocalPort;
        public readonly uint RemoteAddr;
        public readonly uint RemotePort;
        public readonly int OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MibUdpRowOwnerPid
    {
        public readonly uint LocalAddr;
        public readonly uint LocalPort;
        public readonly int OwningPid;
    }

    private static List<MibTcpRowOwnerPid> ReadNativeTcpTable()
    {
        var list = new List<MibTcpRowOwnerPid>();
        var size = 0;

        _ = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
        if (size <= 0)
        {
            return list;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var ret = GetExtendedTcpTable(buffer, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
            if (ret != 0)
            {
                return list;
            }

            var numEntries = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

            for (var i = 0; i < numEntries; i++)
            {
                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                list.Add(row);
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return list;
    }

    private static List<MibUdpRowOwnerPid> ReadNativeUdpTable()
    {
        var list = new List<MibUdpRowOwnerPid>();
        var size = 0;

        _ = GetExtendedUdpTable(IntPtr.Zero, ref size, true, AfInet, UdpTableOwnerPid, 0);
        if (size <= 0)
        {
            return list;
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var ret = GetExtendedUdpTable(buffer, ref size, true, AfInet, UdpTableOwnerPid, 0);
            if (ret != 0)
            {
                return list;
            }

            var numEntries = Marshal.ReadInt32(buffer);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibUdpRowOwnerPid>();

            for (var i = 0; i < numEntries; i++)
            {
                var row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(rowPtr);
                list.Add(row);
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return list;
    }
}
