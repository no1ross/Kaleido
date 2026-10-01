using Kaleido.Authorization;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Process.Context;
using Kaleido.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.UnitTests.Authorization;

public sealed class KaleidoAuthorizationEvaluatorTests
    : SutFixture
{
    private static KaleidoAuthorizationEvaluator CreateSut() =>
        new(NullLogger<KaleidoAuthorizationEvaluator>.Instance);

    private static KaleidoCorrelationContext Caller(
        string? name = null,
        params string[] roles) =>
        new()
        {
            CallerName = name,
            CallerRoles = roles
        };

    private static ProcessorContext OwnedContext(
        string? owner,
        params string[] ownerRoles) =>
        new()
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test",
            Owner = owner,
            OwnerRoles = ownerRoles
        };

    // -- CanAccessAsync ------------------------------------------------------

    [Fact]
    public async Task CanAccessAsync_NoDeclaration_DefaultOpen_AllowsUnauthenticated()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                authorization: null,
                Caller(),
                requireAuthorization: false,
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_NoDeclaration_RequireAuthorization_DeniesAnonymous()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                authorization: null,
                Caller(),
                requireAuthorization: true,
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_NoDeclaration_RequireAuthorization_AllowsAuthenticated()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                authorization: null,
                Caller("alice"),
                requireAuthorization: true,
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_WithRoles_AllowsMatchingRole()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, ["internal"]),
                Caller("bob", "internal"),
                requireAuthorization: false,
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_WithRoles_DeniesMissingRole()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, ["internal"]),
                Caller("bob", "viewer"),
                requireAuthorization: false,
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_WithPolicy_NoEvaluator_Denies()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller("alice"),
                requireAuthorization: false,
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_WithPolicy_InvokesEvaluator()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller("alice"),
                requireAuthorization: false,
                (_, _) => Task.FromResult(true)));
    }

    // -- AuthorizeAsync ------------------------------------------------------

    [Fact]
    public async Task AuthorizeAsync_WhenDenied_ThrowsWithAuthenticationFlag()
    {
        var sut = CreateSut();

        var anonymous = await Assert.ThrowsAsync<KaleidoAuthorizationException>(
            () => sut.AuthorizeAsync(
                null,
                "cap",
                Caller(),
                requireAuthorization: true,
                policyEvaluator: null));

        Assert.False(anonymous.CallerIsAuthenticated);

        var authenticated = await Assert.ThrowsAsync<KaleidoAuthorizationException>(
            () => sut.AuthorizeAsync(
                new AuthorizationMetadata(null, ["internal"]),
                "cap",
                Caller("bob"),
                requireAuthorization: false,
                policyEvaluator: null));

        Assert.True(authenticated.CallerIsAuthenticated);
    }

    // -- AuthorizeProcess ----------------------------------------------------

    [Fact]
    public void AuthorizeProcess_WhenUnowned_PassesForAnonymous()
    {
        var sut = CreateSut();

        sut.AuthorizeProcess(
            OwnedContext(owner: null),
            Caller());
    }

    [Fact]
    public void AuthorizeProcess_WhenOwnedByCaller_Passes()
    {
        var sut = CreateSut();

        sut.AuthorizeProcess(
            OwnedContext("alice", "intake"),
            Caller("alice"));
    }

    [Fact]
    public void AuthorizeProcess_WhenCallerSharesOwnerRole_Passes()
    {
        var sut = CreateSut();

        sut.AuthorizeProcess(
            OwnedContext("alice", "intake"),
            Caller("bob", "intake"));
    }

    [Fact]
    public void AuthorizeProcess_WhenOtherOwnerAndNoSharedRole_Throws403()
    {
        var sut = CreateSut();

        var exception = Assert.Throws<KaleidoAuthorizationException>(
            () => sut.AuthorizeProcess(
                OwnedContext("alice", "intake"),
                Caller("bob", "viewer")));

        Assert.True(exception.CallerIsAuthenticated);
    }

    [Fact]
    public void AuthorizeProcess_WhenOwnedAndAnonymous_Throws401()
    {
        var sut = CreateSut();

        var exception = Assert.Throws<KaleidoAuthorizationException>(
            () => sut.AuthorizeProcess(
                OwnedContext("alice"),
                Caller()));

        Assert.False(exception.CallerIsAuthenticated);
    }
}
