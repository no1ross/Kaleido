using System.Security.Claims;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Observability.UnitTests;

public sealed class KaleidoCallerContextEndpointFilterTests
    : SutFixture
{
    private static KaleidoCallerContextEndpointFilter CreateSut() =>
        new();

    private static DefaultEndpointFilterInvocationContext CreateInvocation(
        Mock<IKaleidoCorrelationContextAccessor> accessor,
        Mock<IKaleidoCorrelationContextInitializer> initializer,
        ClaimsPrincipal? user = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(accessor.Object);
        services.AddSingleton(initializer.Object);

        return new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext
            {
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity()),
                RequestServices = services.BuildServiceProvider(
                    new ServiceProviderOptions
                    {
                        ValidateScopes = true,
                        ValidateOnBuild = true
                    })
            });
    }

    private static ClaimsPrincipal User(
        string name,
        params string[] roles) =>
        new(new ClaimsIdentity(
            roles.Select(r => new Claim(ClaimTypes.Role, r))
                .Append(new Claim(ClaimTypes.Name, name)),
            authenticationType: "test"));

    [Fact]
    public async Task InvokeAsync_WhenAuthenticated_StampsCallerOnCorrelationContext()
    {
        var accessor = new Mock<IKaleidoCorrelationContextAccessor>();
        accessor
            .Setup(x => x.Current)
            .Returns(new KaleidoCorrelationContext { RequestId = "req-1" });

        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();

        var invocation =
            CreateInvocation(accessor, initializer, User("alice", "internal"));

        await CreateSut().InvokeAsync(
            invocation,
            _ => ValueTask.FromResult<object?>(null));

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-1" &&
                    c.CallerName == "alice" &&
                    c.CallerIsAuthenticated &&
                    c.CallerRoles.Count == 1 &&
                    c.CallerRoles.Contains("internal"))),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenAnonymous_LeavesCallerUnset()
    {
        var accessor = new Mock<IKaleidoCorrelationContextAccessor>();
        accessor
            .Setup(x => x.Current)
            .Returns(new KaleidoCorrelationContext { RequestId = "req-1" });

        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();

        var invocation =
            CreateInvocation(accessor, initializer);

        await CreateSut().InvokeAsync(
            invocation,
            _ => ValueTask.FromResult<object?>(null));

        initializer.Verify(
            x => x.Initialize(
                It.Is<KaleidoCorrelationContext>(c =>
                    c.RequestId == "req-1" &&
                    c.CallerName == null &&
                    c.CallerRoles.Count == 0)),
            Times.Once);
    }

    private static async Task<KaleidoCorrelationContext> InitializedWith(ClaimsPrincipal user)
    {
        var accessor = new Mock<IKaleidoCorrelationContextAccessor>();
        accessor.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { RequestId = "req-1" });
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        KaleidoCorrelationContext? initialized = null;
        initializer.Setup(x => x.Initialize(It.IsAny<KaleidoCorrelationContext>())).Callback<KaleidoCorrelationContext>(c => initialized = c);

        await CreateSut().InvokeAsync(CreateInvocation(accessor, initializer, user), _ => ValueTask.FromResult<object?>(null));

        return initialized ?? throw new Xunit.Sdk.XunitException("The caller was not initialized.");
    }

    [Fact]
    public async Task InvokeAsync_StampsTheSubjectClaimAsTheCallerId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "alice"), new Claim("sub", "sub-42"), new Claim(ClaimTypes.NameIdentifier, "nid-7")],
            authenticationType: "test"));

        var context = await InitializedWith(user);

        Assert.Equal("sub-42", context.CallerId);
        Assert.Equal(new KaleidoCaller { Name = "alice", Id = "sub-42" }, context.Caller);
    }

    [Fact]
    public async Task InvokeAsync_WithoutASubjectClaim_UsesTheNameIdentifier()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "alice"), new Claim(ClaimTypes.NameIdentifier, "nid-7")],
            authenticationType: "test"));

        Assert.Equal("nid-7", (await InitializedWith(user)).CallerId);
    }

    [Fact]
    public async Task InvokeAsync_WhenAnonymous_HasNoCallerIdEvenWithClaims()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "sub-42")]));

        var context = await InitializedWith(user);

        Assert.Null(context.CallerId);
        Assert.Null(context.Caller);
    }

    [Fact]
    public async Task InvokeAsync_WithAPrincipalWithoutAnIdentity_LeavesCallerUnset()
    {
        var context = await InitializedWith(new ClaimsPrincipal());

        Assert.Null(context.CallerName);
        Assert.Null(context.CallerId);
        Assert.Empty(context.CallerRoles);
    }

    // #515: authentication is the principal's, not the presence of a name - a subject-only token is
    // authenticated and keeps its roles.
    [Fact]
    public async Task InvokeAsync_WhenAuthenticatedWithoutAName_IsAuthenticated_WithItsRoles()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "sub-42"), new Claim(ClaimTypes.Role, "clerk")],
            authenticationType: "test"));

        var context = await InitializedWith(user);

        Assert.Null(context.CallerName);
        Assert.Equal("sub-42", context.CallerId);
        Assert.True(context.CallerIsAuthenticated);
        Assert.Equal(["clerk"], context.CallerRoles);
    }

    // An unauthenticated principal carrying claims (roles included) is anonymous: no flag, no roles.
    [Fact]
    public async Task InvokeAsync_WhenUnauthenticatedWithClaims_IsNotAuthenticated_AndHasNoRoles()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "mallory"), new Claim(ClaimTypes.Role, "admin")]));

        var context = await InitializedWith(user);

        Assert.False(context.CallerIsAuthenticated);
        Assert.Null(context.CallerName);
        Assert.Empty(context.CallerRoles);
    }

    // #433: the roles are what ASP.NET's IsInRole (RequireRole on the typed routes) sees - each identity's
    // own RoleClaimType - so a JWT handler that keeps "roles" claims is authorized the same everywhere.
    [Fact]
    public async Task InvokeAsync_ReadsRolesFromTheIdentitysRoleClaimType()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("name", "alice"), new Claim("roles", "radiology"), new Claim(ClaimTypes.Role, "not-a-role-here")],
            authenticationType: "jwt",
            nameType: "name",
            roleType: "roles"));

        var context = await InitializedWith(user);

        Assert.Equal(["radiology"], context.CallerRoles);
        Assert.True(user.IsInRole("radiology"));
        Assert.False(user.IsInRole("not-a-role-here"));
    }

    [Fact]
    public async Task InvokeAsync_CollectsTheRolesOfEveryIdentity_Once()
    {
        var user = new ClaimsPrincipal(
        [
            new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice"), new Claim(ClaimTypes.Role, "clerk")], authenticationType: "cookie"),
            new ClaimsIdentity([new Claim("roles", "radiology"), new Claim("roles", "clerk")], authenticationType: "jwt", nameType: "name", roleType: "roles"),
        ]);

        var context = await InitializedWith(user);

        Assert.Equal(["clerk", "radiology"], context.CallerRoles.Order(StringComparer.Ordinal));
    }

    // ADR 0022: the application acting for the caller comes from the principal's actor claim, never a header.
    [Theory]
    [InlineData("act", "{\"sub\":\"intake-service\"}", "intake-service")]
    [InlineData("act", "intake", "intake")]
    [InlineData("act", "{not json", "{not json")]
    [InlineData("act", "{\"iss\":\"idp\"}", "{\"iss\":\"idp\"}")]
    [InlineData("azp", "kaleido-mcp", "kaleido-mcp")]
    [InlineData("appid", "11111111-2222", "11111111-2222")]
    public async Task InvokeAsync_StampsTheCallersActor_FromTheActorClaim(string claimType, string value, string expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "alice"), new Claim(claimType, value)], authenticationType: "test"));

        Assert.Equal(expected, (await InitializedWith(user)).CallerActor);
    }

    [Fact]
    public async Task InvokeAsync_PrefersTheActClaim_OverTheAuthorizedParty()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "alice"), new Claim("azp", "ui-app"), new Claim("act", "radiology")],
            authenticationType: "test"));

        Assert.Equal("radiology", (await InitializedWith(user)).CallerActor);
    }

    [Fact]
    public async Task InvokeAsync_AnAnonymousCaller_OrAPrincipalWithoutActor_HasNoActor()
    {
        Assert.Null((await InitializedWith(new ClaimsPrincipal(new ClaimsIdentity([new Claim("act", "x")])))).CallerActor);
        Assert.Null((await InitializedWith(User("alice"))).CallerActor);
    }

    [Fact]
    public async Task InvokeAsync_RecordsTheInboundCaller_ForTheOutgoingClients()
    {
        var accessor = new Mock<IKaleidoCorrelationContextAccessor>();
        accessor.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var inbound = new Mock<IKaleidoInboundCallerAccessor>();
        var user = User("alice");
        var services = new ServiceCollection();
        services.AddSingleton(accessor.Object);
        services.AddSingleton(Mock.Of<IKaleidoCorrelationContextInitializer>());
        services.AddSingleton(inbound.Object);
        var httpContext = new DefaultHttpContext
        {
            User = user,
            RequestServices = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }),
        };
        httpContext.Request.Headers.Authorization = "Bearer alice";

        await CreateSut().InvokeAsync(new DefaultEndpointFilterInvocationContext(httpContext), _ => ValueTask.FromResult<object?>(null));

        inbound.Verify(x => x.Set(user, "Bearer alice"), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WhenOnlyTheInitializerIsRegistered_DoesNotInitializeAndCallsNext()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var services = new ServiceCollection();
        services.AddSingleton(initializer.Object);
        var invocation = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(
                    new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true })
            });
        var called = false;

        await CreateSut().InvokeAsync(
            invocation,
            _ =>
            {
                called = true;
                return ValueTask.FromResult<object?>(null);
            });

        Assert.True(called);
        initializer.Verify(x => x.Initialize(It.IsAny<KaleidoCorrelationContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_WhenAccessorNotRegistered_CallsNext()
    {
        var services = new ServiceCollection();

        var invocation = new DefaultEndpointFilterInvocationContext(
            new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider()
            });

        var called = false;

        await CreateSut().InvokeAsync(
            invocation,
            _ =>
            {
                called = true;
                return ValueTask.FromResult<object?>(null);
            });

        Assert.True(called);
    }
}
