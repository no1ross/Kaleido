using Kaleido.Exceptions;
using Kaleido.UnitTests;

namespace Kaleido.Server.UnitTests;

public sealed class KaleidoServerOptionsTests : SutFixture<KaleidoServerOptions>
{
    protected override KaleidoServerOptions CreateSut() => new();

    [Fact]
    public void Defaults_AreAuthenticatedAndUnspecified()
    {
        Assert.Equal(KaleidoAuthorizationMode.Authenticated, Sut.AuthorizationMode);
        Assert.False(Sut.DefaultAuthorization.IsExplicit());
    }

    [Fact]
    public void Validate_WhenOptionsAreNull_Throws()
    {
        Assert.Equal("options", Assert.Throws<ArgumentNullException>(() => KaleidoServerOptions.ValidateAuthorization(null!)).ParamName);
    }

    [Fact]
    public void Validate_UnknownMode_ThrowsInvalidAuthorizationMode()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            KaleidoServerOptions.ValidateAuthorization(Sut with { AuthorizationMode = (KaleidoAuthorizationMode)42 }));

        Assert.Equal(ConfigurationErrorCodes.InvalidAuthorizationMode, exception.Code);
        Assert.Contains("Use None, Authenticated, or ZeroTrust", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_NullDefault_ThrowsConflictingAuthorization()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            KaleidoServerOptions.ValidateAuthorization(Sut with { DefaultAuthorization = null! }));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Fact]
    public void Validate_NullRoles_ThrowsConflictingAuthorization()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            KaleidoServerOptions.ValidateAuthorization(Sut with { DefaultAuthorization = new AuthorizationMetadata(null, null!) }));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Theory]
    [InlineData("staff", false)]
    [InlineData(null, true)]
    public void Validate_AnonymousCombinedWithPolicyOrRoles_ThrowsConflictingAuthorization(string? policy, bool withRole)
    {
        var rule = new AuthorizationMetadata(policy, withRole ? ["admin"] : []) { AllowAnonymous = true };

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            KaleidoServerOptions.ValidateAuthorization(Sut with { DefaultAuthorization = rule }));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Fact]
    public void Validate_ValidPolicyAndRoles_DoesNotThrow()
    {
        KaleidoServerOptions.ValidateAuthorization(Sut with { DefaultAuthorization = new AuthorizationMetadata("staff", ["admin"]) });
    }
}
