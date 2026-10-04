using Kaleido.Authorization;
using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Processor.Context;
using Kaleido.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.UnitTests.Authorization;

public sealed class KaleidoAuthorizationEvaluatorTests
    : SutFixture
{
    private static KaleidoAuthorizationEvaluator CreateSut(
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.Authenticated) =>
        new(
            new KaleidoServiceOptions
            {
                ServiceName = "test",
                AuthorizationMode = mode
            },
            NullLogger<KaleidoAuthorizationEvaluator>.Instance);

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

    private static readonly AuthorizationMetadata Anonymous =
        new(null, []) { AllowAnonymous = true };

    // -- IsEnforced ----------------------------------------------------------

    [Fact]
    public void IsEnforced_ReflectsServiceOptions()
    {
        Assert.True(CreateSut(KaleidoAuthorizationMode.Authenticated).IsEnforced);
        Assert.False(CreateSut(KaleidoAuthorizationMode.None).IsEnforced);
    }

    // -- CanAccessAsync: not enforced ----------------------------------------

    [Fact]
    public async Task CanAccessAsync_NotEnforced_NoDeclaration_AllowsAnonymous()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.None);

        Assert.True(
            await sut.CanAccessAsync(
                authorization: null,
                Caller(),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_NotEnforced_WithRoles_AllowsAnonymous()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.None);

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, ["radiology"]),
                Caller(),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_NotEnforced_WithPolicy_DoesNotInvokeEvaluator()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.None);

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller(),
                (_, _) => throw new InvalidOperationException("must not be called")));
    }

    // -- CanAccessAsync: enforced --------------------------------------------

    [Fact]
    public async Task CanAccessAsync_Enforced_NoDeclaration_DeniesAnonymous()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                authorization: null,
                Caller(),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_NoDeclaration_AllowsAuthenticated()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                authorization: null,
                Caller("alice"),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_EmptyDeclaration_DeniesAnonymous()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, []),
                Caller(),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_AllowAnonymous_AllowsAnonymous()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                Anonymous,
                Caller(),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithRoles_AllowsMatchingRole()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, ["radiology"]),
                Caller("bob", "radiology"),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithRoles_DeniesMissingRole()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, ["radiology"]),
                Caller("bob", "viewer"),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithPolicy_NoEvaluator_Denies()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller("alice"),
                policyEvaluator: null));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithPolicy_InvokesEvaluator()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller("alice"),
                (_, _) => Task.FromResult(true)));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithPolicy_DeniesAnonymousBeforeEvaluating()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller(),
                (_, _) => Task.FromResult(true)));
    }

    [Fact]
    public async Task CanAccessAsync_ZeroTrust_DeniesUndeclaredAndEmptyEvenWhenAuthenticated()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.ZeroTrust);

        Assert.False(await sut.CanAccessAsync(null, Caller("alice"), null));
        Assert.False(await sut.CanAccessAsync(new AuthorizationMetadata(null, []), Caller("alice"), null));
    }

    [Fact]
    public async Task CanAccessAsync_ZeroTrust_AllowsExplicitRolePolicyAndAnonymous()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.ZeroTrust);

        Assert.True(await sut.CanAccessAsync(
            new AuthorizationMetadata(null, ["radiology"]), Caller("bob", "radiology"), null));
        Assert.True(await sut.CanAccessAsync(
            new AuthorizationMetadata("can-view", []), Caller("alice"), (_, _) => Task.FromResult(true)));
        Assert.True(await sut.CanAccessAsync(Anonymous, Caller(), null));
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
                policyEvaluator: null));

        Assert.False(anonymous.CallerIsAuthenticated);

        var authenticated = await Assert.ThrowsAsync<KaleidoAuthorizationException>(
            () => sut.AuthorizeAsync(
                new AuthorizationMetadata(null, ["radiology"]),
                "cap",
                Caller("bob"),
                policyEvaluator: null));

        Assert.True(authenticated.CallerIsAuthenticated);
    }

    // -- AuthorizeProcess ----------------------------------------------------

    [Fact]
    public void AuthorizeProcess_NotEnforced_PassesForOtherOwner()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.None);

        sut.AuthorizeProcess(
            OwnedContext("alice", "radiology"),
            Caller());
    }

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
            OwnedContext("alice", "radiology"),
            Caller("alice"));
    }

    [Fact]
    public void AuthorizeProcess_WhenCallerSharesOwnerRole_Passes()
    {
        var sut = CreateSut();

        sut.AuthorizeProcess(
            OwnedContext("alice", "radiology"),
            Caller("bob", "radiology"));
    }

    [Fact]
    public void AuthorizeProcess_WhenOtherOwnerAndNoSharedRole_Throws403()
    {
        var sut = CreateSut();

        var exception = Assert.Throws<KaleidoAuthorizationException>(
            () => sut.AuthorizeProcess(
                OwnedContext("alice", "radiology"),
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
