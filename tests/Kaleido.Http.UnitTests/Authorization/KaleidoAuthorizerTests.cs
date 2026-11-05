using System.Security.Claims;
using Kaleido.Exceptions;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Authorization.UnitTests;

// The authorizer turns a capability's declaration into an ASP.NET policy and asks IAuthorizationService
// (#574): these tests check which policy it asks for, for which principal, and what it answers.
public sealed class KaleidoAuthorizerTests
    : SutFixture
{
    private static readonly ClaimsPrincipal Bob =
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "bob")], "test"));

    private readonly Mock<IAuthorizationService> _authorization = new();
    private readonly Mock<IAuthorizationPolicyProvider> _policies = new();
    private readonly List<(ClaimsPrincipal User, IAuthorizationRequirement[] Requirements)> _asked = [];

    public KaleidoAuthorizerTests() =>
        Answer(succeeded: true);

    private KaleidoAuthorizer CreateSut(
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.Authenticated,
        ClaimsPrincipal? user = null,
        bool withRequest = true,
        ILogger<KaleidoAuthorizer>? logger = null) =>
        new(
            new KaleidoServiceOptions { ServiceName = "test", AuthorizationMode = mode },
            _authorization.Object,
            _policies.Object,
            new HttpContextAccessor { HttpContext = withRequest ? new DefaultHttpContext { User = user ?? Bob } : null },
            logger ?? NullLogger<KaleidoAuthorizer>.Instance);

    private void Answer(bool succeeded) =>
        _authorization
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .Callback<ClaimsPrincipal, object?, IEnumerable<IAuthorizationRequirement>>((user, _, requirements) => _asked.Add((user, [.. requirements])))
            .ReturnsAsync(succeeded ? AuthorizationResult.Success() : AuthorizationResult.Failed());

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(KaleidoAuthorizationMode.None, false)]
    [InlineData(KaleidoAuthorizationMode.Authenticated, true)]
    [InlineData(KaleidoAuthorizationMode.ZeroTrust, true)]
    public void IsEnforced_IsFalseOnlyForNone(KaleidoAuthorizationMode mode, bool enforced) =>
        Assert.Equal(enforced, CreateSut(mode).IsEnforced);

    [Fact]
    public async Task None_AllowsEverything_WithoutAsking()
    {
        Assert.True(await CreateSut(KaleidoAuthorizationMode.None).CanAccessAsync(new AuthorizationMetadata(null, ["admin"]), Cancel));
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task AllowAnonymous_IsAllowed_WithoutAsking()
    {
        Assert.True(await CreateSut().CanAccessAsync(new AuthorizationMetadata(null, []) { AllowAnonymous = true }, Cancel));
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task Undeclared_InAuthenticatedMode_AsksForAnAuthenticatedCaller()
    {
        Assert.True(await CreateSut().CanAccessAsync(null, Cancel));

        var (user, requirements) = Assert.Single(_asked);
        Assert.Same(Bob, user);
        Assert.IsType<DenyAnonymousAuthorizationRequirement>(Assert.Single(requirements));
    }

    [Fact]
    public async Task Undeclared_InZeroTrustMode_IsDenied_WithoutAsking()
    {
        Assert.False(await CreateSut(KaleidoAuthorizationMode.ZeroTrust).CanAccessAsync(AuthorizationMetadata.Unspecified, Cancel));
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task Roles_AreAskedAsTheRequestUsersRoleRequirement()
    {
        Assert.True(await CreateSut().CanAccessAsync(new AuthorizationMetadata(null, ["radiology", "admin"]), Cancel));

        var (user, requirements) = Assert.Single(_asked);
        Assert.Same(Bob, user);
        Assert.Contains(requirements, x => x is DenyAnonymousAuthorizationRequirement);
        Assert.Equal(["radiology", "admin"], Assert.Single(requirements.OfType<RolesAuthorizationRequirement>()).AllowedRoles);
    }

    [Fact]
    public async Task ADeniedRequirement_IsNotAllowed()
    {
        Answer(succeeded: false);

        Assert.False(await CreateSut().CanAccessAsync(new AuthorizationMetadata(null, ["radiology"]), Cancel));
    }

    [Fact]
    public async Task ANamedPolicy_IsCombinedWithTheRoles()
    {
        var hipaa = new ClaimsAuthorizationRequirement("hipaa", allowedValues: null);
        _policies.Setup(x => x.GetPolicyAsync("hipaa")).ReturnsAsync(new AuthorizationPolicy([hipaa], []));

        Assert.True(await CreateSut().CanAccessAsync(new AuthorizationMetadata("hipaa", ["radiology"]), Cancel));

        var requirements = Assert.Single(_asked).Requirements;
        Assert.Contains(hipaa, requirements);
        Assert.Contains(requirements, x => x is RolesAuthorizationRequirement);
    }

    // #553: a policy this host does not define fails closed - not allowed, never an exception - and is logged once.
    [Fact]
    public async Task APolicyThisHostDoesNotDefine_IsNotAllowed_AndLoggedOnce()
    {
        var unknown = $"undefined-{Guid.NewGuid():N}";
        _policies.Setup(x => x.GetPolicyAsync(unknown)).ReturnsAsync((AuthorizationPolicy?)null);
        var logger = new Mock<ILogger<KaleidoAuthorizer>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var sut = CreateSut(logger: logger.Object);

        Assert.False(await sut.CanAccessAsync(new AuthorizationMetadata(unknown, []), Cancel));
        Assert.False(await sut.CanAccessAsync(new AuthorizationMetadata(unknown, []), Cancel));

        Assert.Empty(_asked);
        logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(unknown, StringComparison.Ordinal)), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuthorizeAsync_WhenDenied_ThrowsWithThePrincipalsAuthentication(bool authenticated)
    {
        Answer(succeeded: false);
        var user = authenticated ? Bob : new ClaimsPrincipal(new ClaimsIdentity());

        var denied = await Assert.ThrowsAsync<KaleidoAuthorizationException>(() =>
            CreateSut(user: user).AuthorizeAsync(new AuthorizationMetadata(null, ["radiology"]), "cap", Cancel));

        Assert.Equal("cap", denied.Capability);
        Assert.Equal(authenticated, denied.CallerIsAuthenticated);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAllowed_Returns() =>
        await CreateSut().AuthorizeAsync(new AuthorizationMetadata(null, ["radiology"]), "cap", Cancel);

    // Outside a request nobody is authenticated: the check is asked for an anonymous principal.
    [Fact]
    public async Task WithoutARequest_TheCallerIsAnonymous()
    {
        await CreateSut(withRequest: false).CanAccessAsync(null, Cancel);

        Assert.False(Assert.Single(_asked).User.Identity?.IsAuthenticated);
    }

    [Fact]
    public async Task FilterAsync_WhenEnforced_KeepsTheAllowedItems()
    {
        _authorization
            .Setup(x => x.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), null, It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object? _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<RolesAuthorizationRequirement>().Any() ? AuthorizationResult.Failed() : AuthorizationResult.Success());
        Item[] items = [new("open", null), new("radiology-only", new AuthorizationMetadata(null, ["radiology"]))];

        var filtered = await CreateSut().FilterAsync(items, i => i.Authorization, Cancel);

        Assert.Equal("open", Assert.Single(filtered).Name);
    }

    [Fact]
    public async Task FilterAsync_WhenNotEnforced_KeepsEverything_WithoutAsking()
    {
        Item[] items = [new("open", null), new("radiology-only", new AuthorizationMetadata(null, ["radiology"]))];

        var filtered = await CreateSut(KaleidoAuthorizationMode.None).FilterAsync(items, i => i.Authorization, Cancel);

        Assert.Equal(2, filtered.Count);
        Assert.Empty(_asked);
    }

    private sealed record Item(string Name, AuthorizationMetadata? Authorization);
}
