using System.Security.Claims;
using Kaleido.Authorization;
using Kaleido.Exceptions;
using Kaleido.Http.Authorization;
using Kaleido.Process.Context;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.UnitTests.Authorization;

public sealed class KaleidoAuthorizerTests
    : SutFixture
{
    private static KaleidoAuthorizer CreateSut(
        Mock<IKaleidoAuthorizationEvaluator> evaluator,
        KaleidoCorrelationContext? caller = null,
        bool requireAuthorization = false,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation
            .Setup(x => x.Current)
            .Returns(caller ?? new KaleidoCorrelationContext());

        return new(
            new KaleidoHttpOptions
            {
                RequireAuthorization = requireAuthorization
            },
            evaluator.Object,
            correlation.Object,
            httpContextAccessor ?? new HttpContextAccessor());
    }

    [Fact]
    public async Task CanAccessAsync_ForwardsCallerAndRequireAuthorization_ToEvaluator()
    {
        var evaluator = new Mock<IKaleidoAuthorizationEvaluator>();
        evaluator
            .Setup(x => x.CanAccessAsync(
                It.IsAny<AuthorizationMetadata?>(),
                It.IsAny<KaleidoCorrelationContext>(),
                true,
                It.IsAny<Func<string, CancellationToken, Task<bool>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var caller =
            new KaleidoCorrelationContext { CallerName = "alice" };
        var sut = CreateSut(evaluator, caller, requireAuthorization: true);

        Assert.True(await sut.CanAccessAsync(null));

        evaluator.Verify(
            x => x.CanAccessAsync(
                null,
                It.Is<KaleidoCorrelationContext>(c => c.CallerName == "alice"),
                true,
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AuthorizeAsync_DelegatesToEvaluator()
    {
        var evaluator = new Mock<IKaleidoAuthorizationEvaluator>();

        var sut = CreateSut(evaluator);

        await sut.AuthorizeAsync(
            new AuthorizationMetadata(null, ["internal"]),
            "cap");

        evaluator.Verify(
            x => x.AuthorizeAsync(
                It.Is<AuthorizationMetadata?>(m =>
                    m != null && m.Roles.Contains("internal")),
                "cap",
                It.IsAny<KaleidoCorrelationContext>(),
                false,
                It.IsAny<Func<string, CancellationToken, Task<bool>>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task FilterAsync_ReturnsOnlyAllowedItems()
    {
        var evaluator = new Mock<IKaleidoAuthorizationEvaluator>();
        evaluator
            .Setup(x => x.CanAccessAsync(
                It.IsAny<AuthorizationMetadata?>(),
                It.IsAny<KaleidoCorrelationContext>(),
                It.IsAny<bool>(),
                It.IsAny<Func<string, CancellationToken, Task<bool>>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthorizationMetadata? m, KaleidoCorrelationContext _, bool _, Func<string, CancellationToken, Task<bool>>? _, CancellationToken _)
                => m is null);

        var sut = CreateSut(evaluator);

        var items = new[]
        {
            new Item("open", null),
            new Item("internal-only", new AuthorizationMetadata(null, ["internal"]))
        };

        var filtered =
            await sut.FilterAsync(items, i => i.Authorization);

        var item = Assert.Single(filtered);
        Assert.Equal("open", item.Name);
    }

    [Fact]
    public void AuthorizeProcess_DelegatesToEvaluator()
    {
        var evaluator = new Mock<IKaleidoAuthorizationEvaluator>();
        evaluator
            .Setup(x => x.AuthorizeProcess(
                It.IsAny<ProcessorContext>(),
                It.IsAny<KaleidoCorrelationContext>()))
            .Throws(new KaleidoAuthorizationException("proc", false));

        var sut = CreateSut(evaluator);

        Assert.Throws<KaleidoAuthorizationException>(
            () => sut.AuthorizeProcess(
                new ProcessorContext
                {
                    ProcessId = Guid.NewGuid(),
                    ProcessorName = "test",
                    Owner = "alice"
                }));
    }

    [Fact]
    public async Task CanAccessAsync_WhenAuthorizationServicePresent_SuppliesPolicyEvaluator()
    {
        var authz = new Mock<IAuthorizationService>();
        authz
            .Setup(x => x.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                null,
                "hipaa"))
            .ReturnsAsync(AuthorizationResult.Success());

        Func<string, CancellationToken, Task<bool>>? captured = null;

        var evaluator = new Mock<IKaleidoAuthorizationEvaluator>();
        evaluator
            .Setup(x => x.CanAccessAsync(
                It.IsAny<AuthorizationMetadata?>(),
                It.IsAny<KaleidoCorrelationContext>(),
                It.IsAny<bool>(),
                It.IsAny<Func<string, CancellationToken, Task<bool>>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<AuthorizationMetadata?, KaleidoCorrelationContext, bool, Func<string, CancellationToken, Task<bool>>?, CancellationToken>(
                (_, _, _, pe, _) => captured = pe)
            .ReturnsAsync(true);

        var services = new ServiceCollection();
        services.AddSingleton(authz.Object);

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services.BuildServiceProvider(
                    new ServiceProviderOptions
                    {
                        ValidateScopes = true,
                        ValidateOnBuild = true
                    })
            }
        };

        var sut = CreateSut(
            evaluator,
            httpContextAccessor: httpContextAccessor);

        await sut.CanAccessAsync(
            new AuthorizationMetadata("hipaa", []));

        Assert.NotNull(captured);
        Assert.True(await captured("hipaa", CancellationToken.None));

        authz.VerifyAll();
    }

    [Fact]
    public async Task CanAccessAsync_WhenNoAuthorizationService_PolicyEvaluatorIsNull()
    {
        Func<string, CancellationToken, Task<bool>>? captured = null;

        var evaluator = new Mock<IKaleidoAuthorizationEvaluator>();
        evaluator
            .Setup(x => x.CanAccessAsync(
                It.IsAny<AuthorizationMetadata?>(),
                It.IsAny<KaleidoCorrelationContext>(),
                It.IsAny<bool>(),
                It.IsAny<Func<string, CancellationToken, Task<bool>>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<AuthorizationMetadata?, KaleidoCorrelationContext, bool, Func<string, CancellationToken, Task<bool>>?, CancellationToken>(
                (_, _, _, pe, _) => captured = pe)
            .ReturnsAsync(true);

        var sut = CreateSut(evaluator);

        await sut.CanAccessAsync(
            new AuthorizationMetadata("hipaa", []));

        Assert.Null(captured);
    }

    private sealed record Item(string Name, AuthorizationMetadata? Authorization);
}
