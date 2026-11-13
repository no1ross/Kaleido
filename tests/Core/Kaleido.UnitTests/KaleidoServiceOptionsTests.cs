using Kaleido.Exceptions;
using Kaleido.Testing;

namespace Kaleido.UnitTests;

public sealed class KaleidoServiceOptionsTests : SutFixture<KaleidoServiceOptions>
{
    protected override KaleidoServiceOptions CreateSut() => CreateSut("test-service");

    private static KaleidoServiceOptions CreateSut(string serviceName, bool withAssemblies = true) =>
        new()
        {
            ServiceName = serviceName,
            ServiceVersion = "1",
            Assemblies = withAssemblies ? [typeof(KaleidoServiceOptionsTests).Assembly] : null,
        };

    [Fact]
    public void Defaults_UseAFreshInstanceAndKaleidoSection()
    {
        var sut = Sut;

        Assert.NotEqual(Guid.Empty, sut.InstanceId);
        Assert.Equal("Kaleido", ((IKaleidoOptions)sut).ConfigurationPath);
    }

    [Fact]
    public void Validate_ValidOptions_DoesNotThrow()
    {
        KaleidoServiceOptions.Validate(Sut);
        KaleidoServiceOptions.Validate(CreateSut("svc"));
    }

    [Fact]
    public void Validate_WhenOptionsIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => KaleidoServiceOptions.Validate(null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_EmptyServiceName_ThrowsInvalidServiceName(string serviceName)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut(serviceName)));

        Assert.Equal(ConfigurationErrorCodes.InvalidServiceName, exception.Code);
        Assert.Contains("non-empty", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ServiceNameCases.Valid), MemberType = typeof(ServiceNameCases))]
    public void Validate_ValidServiceName_DoesNotThrow(string serviceName)
    {
        KaleidoServiceOptions.Validate(CreateSut(serviceName));
    }

    [Theory]
    [MemberData(nameof(ServiceNameCases.Invalid), MemberType = typeof(ServiceNameCases))]
    public void Validate_InvalidServiceName_ThrowsInvalidServiceName(string serviceName)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut(serviceName)));

        Assert.Equal(ConfigurationErrorCodes.InvalidServiceName, exception.Code);
    }

    // The same rule, shared with Kaleido.Http.Client for route prefixes (#434).
    [Theory]
    [MemberData(nameof(ServiceNameCases.Valid), MemberType = typeof(ServiceNameCases))]
    public void IsValidServiceName_ValidName_IsTrue(string serviceName)
    {
        Assert.True(KaleidoServiceOptions.IsValidServiceName(serviceName));
    }

    [Theory]
    [MemberData(nameof(ServiceNameCases.Invalid), MemberType = typeof(ServiceNameCases))]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("../member")]
    [InlineData("member/x")]
    public void IsValidServiceName_InvalidName_IsFalse(string? serviceName)
    {
        Assert.False(KaleidoServiceOptions.IsValidServiceName(serviceName));
    }

    [Theory]
    [InlineData("MyService", "myservice")]
    [InlineData("My Svc.V2", "mysvcv2")]
    [InlineData("-intake", "intake")]
    public void Validate_InvalidServiceName_SuggestsAValidName(string serviceName, string suggestion)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut(serviceName)));

        Assert.Contains($"Use '{suggestion}' instead.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NameWithNothingToKeep_HasNoSuggestion()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("..")));

        Assert.DoesNotContain("instead", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WithoutAssemblies_ThrowsMissingAssembly()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc", withAssemblies: false)));

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
    }

    // The version is an opaque label in every store key, and a route segment later (#552, #553).
    [Theory]
    [InlineData("1")]
    [InlineData("v2")]
    [InlineData("2026.11-beta_1")]
    [InlineData("Lorem")]
    public void Validate_ValidServiceVersion_DoesNotThrow(string serviceVersion)
    {
        KaleidoServiceOptions.Validate(CreateSut("svc") with { ServiceVersion = serviceVersion });
    }

    [Fact]
    public void Validate_NoServiceVersion_ThrowsInvalidServiceVersion()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            KaleidoServiceOptions.Validate(CreateSut("svc") with { ServiceVersion = null! }));

        Assert.Equal(ConfigurationErrorCodes.InvalidServiceVersion, exception.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-1")]
    [InlineData(".1")]
    [InlineData("v 2")]
    [InlineData("v/2")]
    [InlineData("vé")]
    public void Validate_InvalidServiceVersion_ThrowsInvalidServiceVersion(string serviceVersion)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc") with { ServiceVersion = serviceVersion }));

        Assert.Equal(ConfigurationErrorCodes.InvalidServiceVersion, exception.Code);
    }

    [Fact]
    public void Validate_ServiceVersionAtTheMaximumLength_DoesNotThrow()
    {
        KaleidoServiceOptions.Validate(CreateSut("svc") with { ServiceVersion = new string('1', KaleidoServiceOptions.MaxServiceVersionLength) });
    }

    [Fact]
    public void Validate_ServiceVersionOverTheMaximumLength_ThrowsInvalidServiceVersion()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc") with { ServiceVersion = new string('1', KaleidoServiceOptions.MaxServiceVersionLength + 1) }));

        Assert.Equal(ConfigurationErrorCodes.InvalidServiceVersion, exception.Code);
    }

    // The scope name keys stores shared by services and versions (#552); '@' is in neither a name nor a version.
    [Theory]
    [InlineData("1", "svc@1")]
    [InlineData("2", "svc@2")]
    public void ScopeName_IsTheServiceAndItsVersion(string version, string expected)
    {
        Assert.Equal(expected, (CreateSut("svc") with { ServiceVersion = version }).ScopeName);
    }
}
