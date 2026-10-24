using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Authorization.UnitTests;

public sealed class KaleidoAuthorizationEvaluatorTests
    : SutFixture
{
    private static KaleidoAuthorizationEvaluator CreateSut(
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.Authenticated,
        Microsoft.Extensions.Logging.ILogger<KaleidoAuthorizationEvaluator>? logger = null) =>
        new(
            new KaleidoServiceOptions
            {
                ServiceName = "test",
                AuthorizationMode = mode
            },
            logger ?? NullLogger<KaleidoAuthorizationEvaluator>.Instance);

    private static KaleidoCorrelationContext Caller(
        string? name = null,
        params string[] roles) =>
        new()
        {
            CallerName = name,
            CallerRoles = roles
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
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_NotEnforced_WithRoles_AllowsAnonymous()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.None);

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, ["radiology"]),
                Caller(),
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_NotEnforced_WithPolicy_DoesNotInvokeEvaluator()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.None);

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller(),
                (_, _) => throw new InvalidOperationException("must not be called"), TestContext.Current.CancellationToken));
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
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_NoDeclaration_AllowsAuthenticated()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                authorization: null,
                Caller("alice"),
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_EmptyDeclaration_DeniesAnonymous()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, []),
                Caller(),
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_AllowAnonymous_AllowsAnonymous()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                Anonymous,
                Caller(),
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithRoles_AllowsMatchingRole()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, ["radiology"]),
                Caller("bob", "radiology"),
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithRoles_DeniesMissingRole()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata(null, ["radiology"]),
                Caller("bob", "viewer"),
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithPolicy_NoEvaluator_Denies()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller("alice"),
                policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithPolicy_InvokesEvaluator()
    {
        var sut = CreateSut();

        Assert.True(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller("alice"),
                (_, _) => Task.FromResult(true), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithPolicy_DeniesAnonymousBeforeEvaluating()
    {
        var sut = CreateSut();

        Assert.False(
            await sut.CanAccessAsync(
                new AuthorizationMetadata("can-view", []),
                Caller(),
                (_, _) => Task.FromResult(true), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_ZeroTrust_DeniesUndeclaredAndEmptyEvenWhenAuthenticated()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.ZeroTrust);

        Assert.False(await sut.CanAccessAsync(null, Caller("alice"), null, TestContext.Current.CancellationToken));
        Assert.False(await sut.CanAccessAsync(new AuthorizationMetadata(null, []), Caller("alice"), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_ZeroTrust_AllowsExplicitRolePolicyAndAnonymous()
    {
        var sut = CreateSut(KaleidoAuthorizationMode.ZeroTrust);

        Assert.True(await sut.CanAccessAsync(
            new AuthorizationMetadata(null, ["radiology"]), Caller("bob", "radiology"), null, TestContext.Current.CancellationToken));
        Assert.True(await sut.CanAccessAsync(
            new AuthorizationMetadata("can-view", []), Caller("alice"), (_, _) => Task.FromResult(true), TestContext.Current.CancellationToken));
        Assert.True(await sut.CanAccessAsync(Anonymous, Caller(), null, TestContext.Current.CancellationToken));
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
                policyEvaluator: null, TestContext.Current.CancellationToken));

        Assert.False(anonymous.CallerIsAuthenticated);

        var authenticated = await Assert.ThrowsAsync<KaleidoAuthorizationException>(
            () => sut.AuthorizeAsync(
                new AuthorizationMetadata(null, ["radiology"]),
                "cap",
                Caller("bob"),
                policyEvaluator: null, TestContext.Current.CancellationToken));

        Assert.True(authenticated.CallerIsAuthenticated);
    }

    [Fact]
    public async Task AuthorizeAsync_WhenAllowed_Completes()
    {
        var sut = CreateSut();

        await sut.AuthorizeAsync(new AuthorizationMetadata(null, ["radiology"]), "cap", Caller("bob", "radiology"), policyEvaluator: null, TestContext.Current.CancellationToken);
    }

    // Roles compare ordinally, as ASP.NET's RequireRole does on the typed routes (#433): a role that
    // differs only in case is a different role.
    [Theory]
    [InlineData("RADIOLOGY")]
    [InlineData("Radiology")]
    [InlineData("radiology ")]
    public async Task CanAccessAsync_WhenTheRoleDiffersInCase_Denies(string callerRole)
    {
        var allowed = await CreateSut().CanAccessAsync(
            new AuthorizationMetadata(null, ["radiology"]),
            Caller("bob", callerRole),
            policyEvaluator: null,
            TestContext.Current.CancellationToken);

        Assert.False(allowed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task AuthorizeAsync_WithoutACapabilityName_Throws(string? capabilityName)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => CreateSut().AuthorizeAsync(null, capabilityName!, Caller("bob"), policyEvaluator: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanAccessAsync_WithoutACaller_Throws()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().CanAccessAsync(null, null!, null, TestContext.Current.CancellationToken));

        Assert.Equal("caller", exception.ParamName);
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WhenThePolicyIsNotSatisfied_Denies()
    {
        using var cancellation = new CancellationTokenSource();
        string? evaluatedPolicy = null;
        CancellationToken evaluatedToken = default;

        var allowed = await CreateSut().CanAccessAsync(
            new AuthorizationMetadata("can-view", []),
            Caller("alice"),
            (policy, token) =>
            {
                evaluatedPolicy = policy;
                evaluatedToken = token;
                return Task.FromResult(false);
            },
            cancellation.Token);

        Assert.False(allowed);
        Assert.Equal("can-view", evaluatedPolicy);
        Assert.Equal(cancellation.Token, evaluatedToken);
    }

    [Fact]
    public async Task CanAccessAsync_Enforced_WithPolicy_NoEvaluator_LogsAWarning()
    {
        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<KaleidoAuthorizationEvaluator>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<Microsoft.Extensions.Logging.LogLevel>())).Returns(true);

        await CreateSut(logger: logger.Object).CanAccessAsync(new AuthorizationMetadata("can-view", []), Caller("alice"), policyEvaluator: null, TestContext.Current.CancellationToken);

        var logged = Assert.Single(logger.Invocations, x => x.Method.Name == nameof(Microsoft.Extensions.Logging.ILogger.Log));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, logged.Arguments[0]);
        Assert.Contains("requires policy 'can-view'", logged.Arguments[2].ToString(), StringComparison.Ordinal);
    }
}
