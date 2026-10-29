using ProcessForge.Contracts;
using SocketForge.Contracts;
using SocketForge.Linux;
using SocketForge.Windows;

namespace SocketForge.Factories;

/// <summary>
/// Creates the socket scanner that matches the host operating system.
/// </summary>
public static class SocketScannerFactory
{
    /// <summary>
    /// Creates the socket scanner for the operating system the process runs on.
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

        throw new PlatformNotSupportedException($"The {Environment.OSVersion.Platform} operating system is not supported.");
    }
}
