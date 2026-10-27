using System.Reflection;
using System.Reflection.Emit;

namespace Kaleido.Http.Mcp.UnitTests;

public sealed class KaleidoMcpServerVersionTests
    : Kaleido.UnitTests.SutFixture
{
    private static AssemblyBuilder HostAssembly(string? informationalVersion, Version? version)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Host{Guid.NewGuid():N}") { Version = version },
            AssemblyBuilderAccess.Run);

        if (informationalVersion is not null)
        {
            var constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])
                ?? throw new InvalidOperationException("AssemblyInformationalVersionAttribute(string) not found.");
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [informationalVersion]));
        }

        return assembly;
    }

    // A version the host sets is reported exactly as given, whatever its application's version is.
    [Fact]
    public void Resolve_AConfiguredVersion_IsReportedAsGiven()
    {
        var resolved = KaleidoMcpServerVersion.Resolve(new KaleidoMcpOptions { ServerVersion = "2.1.0-beta" }, HostAssembly("9.9.9", new Version(9, 9)));

        Assert.Equal("2.1.0-beta", resolved.Value);
    }

    [Theory]
    [InlineData("3.4.5+0123abc", "3.4.5")]
    [InlineData("3.4.5-preview.12", "3.4.5-preview.12")]
    public void Resolve_WithoutAConfiguredVersion_ReportsTheHostsInformationalVersion_WithoutTheCommit(string informational, string expected)
    {
        Assert.Equal(expected, KaleidoMcpServerVersion.Resolve(new KaleidoMcpOptions(), HostAssembly(informational, new Version(1, 2))).Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("+0123abc")]
    public void Resolve_WithoutAnInformationalVersion_ReportsTheHostsAssemblyVersion(string? informational)
    {
        Assert.Equal("1.2.3.4", KaleidoMcpServerVersion.Resolve(new KaleidoMcpOptions(), HostAssembly(informational, new Version(1, 2, 3, 4))).Value);
    }

    [Fact]
    public void Resolve_WithoutAHostAssembly_ReportsTheFallback()
    {
        Assert.Equal(KaleidoMcpServerVersion.Fallback, KaleidoMcpServerVersion.Resolve(new KaleidoMcpOptions(), entryAssembly: null).Value);
    }

    [Fact]
    public void Resolve_WithoutOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => KaleidoMcpServerVersion.Resolve(null!, entryAssembly: null));
    }
}
