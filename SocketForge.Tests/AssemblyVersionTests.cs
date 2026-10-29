using System.Reflection;
using AwesomeAssertions;
using SocketForge.Factories;
using Xunit;

namespace SocketForge.Tests;

public sealed class AssemblyVersionTests
{
    /// <summary>
    /// Consumers compiled against an earlier version of the package reference its assembly version:
    /// if it fell back to Major.0.0.0 (the MinVer default), the runtime would refuse to load it.
    /// </summary>
    [Fact]
    public void AssemblyVersion_WhenBuiltFromGitTag_MatchesThePackageVersion()
    {
        // Arrange
        var assembly = typeof(SocketScannerFactory).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        informationalVersion.Should().NotBeNullOrWhiteSpace();
        var packageVersion = Version.Parse(informationalVersion!.Split('+', '-')[0]);

        // Act
        var assemblyVersion = assembly.GetName().Version;

        // Assert
        assemblyVersion.Should().Be(new Version(packageVersion.Major, packageVersion.Minor, packageVersion.Build, 0));
    }
}
