using Kaleido.Exceptions;
using Kaleido.Registry;

namespace Kaleido.UnitTests.Registry;

public sealed class AuthorizationMetadataTests
    : SutFixture
{
    private static readonly string[] InternalAdminRoles = ["internal", "admin"];
    private static readonly string[] SpacedRolesExpected = ["a", "b", "c"];

    [Fact]
    public void ForType_WhenAttributeMissing_ReturnsNull()
    {
        var metadata =
            AuthorizationMetadata.ForType(
                typeof(Unsecured));

        Assert.Null(metadata);
    }

    [Fact]
    public void ForType_WithPolicyOnly_ReturnsPolicyAndEmptyRoles()
    {
        var metadata =
            AuthorizationMetadata.ForType(
                typeof(PolicySecured));

        Assert.NotNull(metadata);
        Assert.Equal("internal-policy", metadata.Policy);
        Assert.Empty(metadata.Roles);
    }

    [Fact]
    public void ForType_WithRolesOnly_ReturnsRolesAndNullPolicy()
    {
        var metadata =
            AuthorizationMetadata.ForType(
                typeof(RoleSecured));

        Assert.NotNull(metadata);
        Assert.Null(metadata.Policy);
        Assert.Equal(
            InternalAdminRoles,
            metadata.Roles);
    }

    [Fact]
    public void ForType_WithPolicyAndRoles_ReturnsBoth()
    {
        var metadata =
            AuthorizationMetadata.ForType(
                typeof(FullySecured));

        Assert.NotNull(metadata);
        Assert.Equal("hipaa", metadata.Policy);
        Assert.Equal(
            "clinician",
            Assert.Single(metadata.Roles));
    }

    [Fact]
    public void ForType_WithCommaDelimitedRoles_SplitsAndTrims()
    {
        var metadata =
            AuthorizationMetadata.ForType(
                typeof(SpacedRoles));

        Assert.NotNull(metadata);
        Assert.Equal(
            SpacedRolesExpected,
            metadata.Roles);
    }

    [Fact]
    public void ForType_WithAllowAnonymous_ReturnsAllowAnonymous()
    {
        var metadata =
            AuthorizationMetadata.ForType(
                typeof(Public));

        Assert.NotNull(metadata);
        Assert.True(metadata.AllowAnonymous);
        Assert.Null(metadata.Policy);
        Assert.Empty(metadata.Roles);
    }

    [Fact]
    public void ForType_WithRolesOnly_AllowAnonymousIsFalse()
    {
        var metadata =
            AuthorizationMetadata.ForType(
                typeof(RoleSecured));

        Assert.NotNull(metadata);
        Assert.False(metadata.AllowAnonymous);
    }

    [Theory]
    [InlineData(typeof(AnonymousWithRoles))]
    [InlineData(typeof(AnonymousWithPolicy))]
    public void ForType_WithAllowAnonymousAndRolesOrPolicy_ThrowsConflictingAuthorization(
        Type type)
    {
        var exception =
            Assert.Throws<KaleidoConfigurationException>(
                () => AuthorizationMetadata.ForType(type));

        Assert.Equal(
            ConfigurationErrorCodes.ConflictingAuthorization,
            exception.Code);
    }

    private sealed class Unsecured;

    [KaleidoAuthorization(AllowAnonymous = true)]
    private sealed class Public;

    [KaleidoAuthorization(AllowAnonymous = true, Roles = "admin")]
    private sealed class AnonymousWithRoles;

    [KaleidoAuthorization(AllowAnonymous = true, Policy = "p")]
    private sealed class AnonymousWithPolicy;

    [KaleidoAuthorization(Policy = "internal-policy")]
    private sealed class PolicySecured;

    [KaleidoAuthorization(Roles = "internal,admin")]
    private sealed class RoleSecured;

    [KaleidoAuthorization(Policy = "hipaa", Roles = "clinician")]
    private sealed class FullySecured;

    [KaleidoAuthorization(Roles = " a , b ,, c ")]
    private sealed class SpacedRoles;
}
