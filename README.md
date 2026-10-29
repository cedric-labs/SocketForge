# SocketForge

Cross-platform active TCP and UDP socket scanner for .NET, inspecting listening ports, established connections, and binding owning process metadata across Windows and Linux.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Framework](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![Documentation: English](https://img.shields.io/badge/Docs-English-blue.svg)](FRAMEWORK.md)
[![Documentation: Français](https://img.shields.io/badge/Docs-Français-green.svg)](FRAMEWORK.fr.md)

> 📖 **Framework Documentation**: [English Specification](FRAMEWORK.md) | [Spécification en français](FRAMEWORK.fr.md)

---

## Key Capabilities

* **Native Kernel Inspections**:
  * **Windows**: Uses Win32 IP Helper API (`GetExtendedTcpTable`, `GetExtendedUdpTable`) for fast, non-blocking socket enumeration.
  * **Linux**: Parses kernel virtual filesystem entries (`/proc/net/tcp`, `/proc/net/tcp6`, `/proc/net/udp`, `/proc/net/udp6`).
* **Process Attribution**: Binds local network endpoints to the specific process ID (PID) and process metadata responsible for network communication.
* **Full Socket State Coverage**:
  * Listening endpoints (`Listen`, `Bind`).
  * Active connections (`Established`, `TimeWait`, `CloseWait`, `SynSent`).
* **IPv4 & IPv6 Dual-Stack Support**: Transparent handling of both IPv4 and IPv6 network sockets.

---

## Installation

Add the package via the .NET CLI:

```powershell
dotnet add package SocketForge --version 1.1.1
```

---

## Code Example

```csharp
using SocketForge.Scanning;

var scanner = new ActiveSocketScanner();
var sockets = await scanner.ScanActiveSocketsAsync();

foreach (var socket in sockets)
{
    Console.WriteLine($"[{socket.Protocol}] {socket.LocalAddress}:{socket.LocalPort} -> {socket.RemoteAddress}:{socket.RemotePort} (PID: {socket.ProcessId}, State: {socket.State})");
}
```

---

## License

This project is licensed under the [MIT License](LICENSE).
