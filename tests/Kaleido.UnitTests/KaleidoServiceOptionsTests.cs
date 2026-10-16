using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Registry;

namespace Kaleido.UnitTests;

public sealed class KaleidoServiceOptionsTests : SutFixture<KaleidoServiceOptions>
{
    protected override KaleidoServiceOptions CreateSut() => CreateSut("test-service");

    private static KaleidoServiceOptions CreateSut(
        string serviceName,
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None,
        AuthorizationMetadata? defaultAuthorization = null,
        bool useNullAuthorization = false,
        KaleidoEventOptions? events = null,
        bool withAssemblies = true) =>
        new()
        {
            ServiceName = serviceName,
            AuthorizationMode = mode,
            DefaultAuthorization = useNullAuthorization ? null! : defaultAuthorization ?? AuthorizationMetadata.Unspecified,
            Events = events ?? new KaleidoEventOptions(),
            Assemblies = withAssemblies ? [typeof(KaleidoServiceOptionsTests).Assembly] : null,
        };

    [Fact]
    public void Defaults_AreUnspecifiedAuthorizationAndFreshInstance()
    {
        var sut = Sut;

        Assert.Equal(KaleidoAuthorizationMode.None, sut.AuthorizationMode);
        Assert.False(sut.DefaultAuthorization.IsExplicit());
        Assert.NotEqual(Guid.Empty, sut.InstanceId);
        Assert.Equal("Kaleido", KaleidoServiceOptions.SectionName);
    }

    [Fact]
    public void Validate_ValidOptions_DoesNotThrow()
    {
        KaleidoServiceOptions.Validate(Sut);
        KaleidoServiceOptions.Validate(CreateSut("svc", KaleidoAuthorizationMode.ZeroTrust, new AuthorizationMetadata(null, []) { AllowAnonymous = true }));
    }

    [Fact]
    public void Validate_WhenOptionsIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => KaleidoServiceOptions.Validate(null!));

        Assert.Equal("options", exception.ParamName);
    }

    [Fact]
    public void Validate_UnknownAuthorizationMode_ThrowsInvalidAuthorizationMode()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc", (KaleidoAuthorizationMode)42)));

        Assert.Equal(ConfigurationErrorCodes.InvalidAuthorizationMode, exception.Code);
    }

    [Fact]
    public void Validate_NullDefaultAuthorization_ThrowsConflictingAuthorization()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc", useNullAuthorization: true)));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Fact]
    public void Validate_NullDefaultAuthorizationRoles_ThrowsConflictingAuthorization()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc", defaultAuthorization: new AuthorizationMetadata(null, null!))));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Theory]
    [InlineData("staff", false)]
    [InlineData(null, true)]
    public void Validate_AnonymousCombinedWithPolicyOrRoles_ThrowsConflictingAuthorization(string? policy, bool withRole)
    {
        var authorization = new AuthorizationMetadata(policy, withRole ? ["admin"] : []) { AllowAnonymous = true };

        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc", defaultAuthorization: authorization)));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Fact]
    public void Validate_PolicyWithoutAnonymous_DoesNotThrow()
    {
        KaleidoServiceOptions.Validate(CreateSut("svc", defaultAuthorization: new AuthorizationMetadata("staff", ["admin"])));
    }

    [Theory]
    [InlineData("", "non-empty")]
    [InlineData("  ", "non-empty")]
    [InlineData("a/b", "path separators")]
    [InlineData("a\\b", "path separators")]
    [InlineData("a b", "whitespace")]
    [InlineData("MyService", "lowercase")]
    public void Validate_InvalidServiceName_ThrowsInvalidServiceName(string serviceName, string reason)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut(serviceName)));

        Assert.Equal(ConfigurationErrorCodes.InvalidServiceName, exception.Code);
        Assert.Contains(reason, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_UppercaseServiceName_SuggestsTheLowercaseName()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("MyService")));

        Assert.Contains("Use 'myservice' instead.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_InvalidEventOptions_ThrowsInvalidEventOptions()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc", events: new KaleidoEventOptions { BatchSize = 0 })));

        Assert.Equal(ConfigurationErrorCodes.InvalidEventOptions, exception.Code);
    }

    [Fact]
    public void Validate_WithoutAssemblies_ThrowsMissingAssembly()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoServiceOptions.Validate(CreateSut("svc", withAssemblies: false)));

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
    }
}
