using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Registry;
using Kaleido.Testing;

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
