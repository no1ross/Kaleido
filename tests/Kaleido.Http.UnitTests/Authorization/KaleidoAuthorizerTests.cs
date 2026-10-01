using System.Security.Claims;
using Kaleido.Exceptions;
using Kaleido.Http.Authorization;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.UnitTests.Authorization;

public sealed class KaleidoAuthorizerTests
    : SutFixture
{
    private static KaleidoAuthorizer CreateSut(
        bool requireAuthorization = false) =>
        new(
            new KaleidoHttpOptions
            {
                RequireAuthorization = requireAuthorization
            },
            NullLogger<KaleidoAuthorizer>.Instance);

    private static DefaultHttpContext CreateContext(
        ClaimsPrincipal? user = null,
        IAuthorizationService? authorizationService = null)
    {
        var services = new ServiceCollection();

        if (authorizationService is not null)
        {
            services.AddSingleton(authorizationService);
        }

        return new DefaultHttpContext
        {
            User = user ?? new ClaimsPrincipal(new ClaimsIdentity()),
            RequestServices =
                services.BuildServiceProvider(
                    new ServiceProviderOptions
                    {
                        ValidateScopes = true,
                        ValidateOnBuild = true
                    })
        };
    }

    private static ClaimsPrincipal AuthenticatedUser(params string[] roles) =>
        new(new ClaimsIdentity(
            roles.Select(r => new Claim(ClaimTypes.Role, r)),
            authenticationType: "test"));

    [Fact]
    public async Task CanAccessAsync_NoDeclaration_DefaultOpen_AllowsUnauthenticated()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                CreateContext(),
                authorization: null));
    }

    [Fact]
    public async Task CanAccessAsync_NoDeclaration_RequireAuthorization_RejectsUnauthenticated()
    {
        var sut = CreateSut(requireAuthorization: true);

        Assert.False(
            await sut.CanAccessAsync(
                CreateContext(),
                authorization: null));
    }

    [Fact]
    public async Task CanAccessAsync_NoDeclaration_RequireAuthorization_AllowsAuthenticated()
    {
        var sut = CreateSut(requireAuthorization: true);

        Assert.True(
            await sut.CanAccessAsync(
                CreateContext(AuthenticatedUser()),
                authorization: null));
    }

    [Fact]
    public async Task CanAccessAsync_Roles_AllowsCallerInAnyRole()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                CreateContext(AuthenticatedUser("internal")),
                new AuthorizationMetadata(null, ["internal", "admin"])));
    }

    [Fact]
    public async Task CanAccessAsync_Roles_RejectsCallerOutsideRoles()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                CreateContext(AuthenticatedUser("viewer")),
                new AuthorizationMetadata(null, ["internal", "admin"])));
    }

    [Fact]
    public async Task CanAccessAsync_Policy_InvokesAuthorizationService()
    {
        var authz = new Mock<IAuthorizationService>();
        authz
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                null,
                "hipaa"))
            .ReturnsAsync(AuthorizationResult.Success());

        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                CreateContext(
                    AuthenticatedUser(),
                    authz.Object),
                new AuthorizationMetadata("hipaa", [])));

        authz.VerifyAll();
    }

    [Fact]
    public async Task CanAccessAsync_PolicyDenied_ReturnsFalse()
    {
        var authz = new Mock<IAuthorizationService>();
        authz
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                null,
                "hipaa"))
            .ReturnsAsync(AuthorizationResult.Failed());

        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                CreateContext(
                    AuthenticatedUser(),
                    authz.Object),
                new AuthorizationMetadata("hipaa", [])));
    }

    [Fact]
    public async Task CanAccessAsync_PolicyWithoutAuthorizationService_FailsClosed()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                CreateContext(AuthenticatedUser()),
                new AuthorizationMetadata("hipaa", [])));
    }

    [Fact]
    public async Task AuthorizeAsync_Unauthenticated_ThrowsUnauthorized()
    {
        var sut = CreateSut();

        var exception =
            await Assert.ThrowsAsync<KaleidoAuthorizationException>(
                () => sut.AuthorizeAsync(
                    CreateContext(),
                    new AuthorizationMetadata(null, ["internal"]),
                    "capture-intake"));

        Assert.False(exception.CallerIsAuthenticated);
        Assert.Equal("capture-intake", exception.Capability);
    }

    [Fact]
    public async Task AuthorizeAsync_AuthenticatedButDenied_ThrowsForbidden()
    {
        var sut = CreateSut();

        var exception =
            await Assert.ThrowsAsync<KaleidoAuthorizationException>(
                () => sut.AuthorizeAsync(
                    CreateContext(AuthenticatedUser("viewer")),
                    new AuthorizationMetadata(null, ["internal"]),
                    "capture-intake"));

        Assert.True(exception.CallerIsAuthenticated);
    }

    [Fact]
    public async Task FilterAsync_ReturnsOnlyAuthorizedItems()
    {
        var sut = CreateSut();

        var items = new[]
        {
            new Item("open", null),
            new Item("internal-only", new AuthorizationMetadata(null, ["internal"]))
        };

        var filtered =
            await sut.FilterAsync(
                CreateContext(AuthenticatedUser("viewer")),
                items,
                i => i.Authorization);

        var item = Assert.Single(filtered);
        Assert.Equal("open", item.Name);
    }

    private sealed record Item(string Name, AuthorizationMetadata? Authorization);
}
