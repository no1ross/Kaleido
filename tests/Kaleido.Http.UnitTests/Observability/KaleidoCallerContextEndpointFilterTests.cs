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

    private static EndpointFilterInvocationContext CreateInvocation(
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
