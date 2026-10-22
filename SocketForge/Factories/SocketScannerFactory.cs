using ProcessForge.Contracts;
using SocketForge.Contracts;
using SocketForge.Linux;
using SocketForge.Windows;

namespace SocketForge.Factories;

/// <summary>
/// Fabrique de résolveurs de sockets adaptés au système d'exploitation hôte.
/// </summary>
public static class SocketScannerFactory
{
    /// <summary>
    /// Crée l'instance de scanneur de sockets adaptée au système d'exploitation d'exécution.
    /// </summary>
    public static ISocketScanner Create(IProcessInspector processInspector)
    {
        ArgumentNullException.ThrowIfNull(processInspector);

        if (OperatingSystem.IsWindows())
        {
            return new WindowsSocketScanner(processInspector);
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxProcSocketScanner(processInspector);
        }

        throw new PlatformNotSupportedException($"Le système d'exploitation {Environment.OSVersion.Platform} n'est pas pris en charge.");
    }
}
