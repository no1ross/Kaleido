using System.Security.Claims;
using Kaleido.Http.Observability;
using Kaleido.Observability;
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

    [Fact]
    public async Task InvokeAsync_WhenAuthenticatedWithoutAName_PublishesNoRoles()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "sub-42"), new Claim(ClaimTypes.Role, "clerk")],
            authenticationType: "test"));

        var context = await InitializedWith(user);

        Assert.Null(context.CallerName);
        Assert.Equal("sub-42", context.CallerId);
        Assert.Empty(context.CallerRoles);
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
