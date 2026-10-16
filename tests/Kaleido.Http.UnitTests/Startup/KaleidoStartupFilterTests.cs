using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Startup.UnitTests;

public sealed class KaleidoStartupFilterTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoStartupFilter CreateSut() => new();

    [Fact]
    public void Configure_ReturnsNonNullAction()
    {
        var sut = CreateSut();
        var result = sut.Configure(_ => { });
        Assert.NotNull(result);
    }

    [Fact]
    public void Configure_InvokesTheNextConfigurationWithTheSameBuilder()
    {
        var builder = new ApplicationBuilder(CreateApplicationServices(Mock.Of<IKaleidoExceptionHandler>()));
        IApplicationBuilder? received = null;

        CreateSut().Configure(app => received = app)(builder);

        Assert.Same(builder, received);
    }

    [Fact]
    public async Task Configure_PutsTheExceptionMiddlewareAroundTheRestOfThePipeline()
    {
        var handler = new Mock<IKaleidoExceptionHandler>();
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var builder = new ApplicationBuilder(CreateApplicationServices(handler.Object));
        var failure = new InvalidOperationException("boom");

        CreateSut().Configure(app => app.Run(_ => throw failure))(builder);
        var pipeline = builder.Build();

        var context = new DefaultHttpContext
        {
            RequestServices = CreateRequestServices(initializer.Object),
        };

        await pipeline(context);

        initializer.Verify(x => x.Initialize(It.IsAny<KaleidoCorrelationContext>()), Times.Once);
        handler.Verify(x => x.HandleAsync(context, failure, context.RequestAborted), Times.Once);
    }

    [Fact]
    public async Task Configure_RunsTheObservabilityMiddlewareBeforeTheNextConfiguration()
    {
        var initializer = new Mock<IKaleidoCorrelationContextInitializer>();
        var builder = new ApplicationBuilder(CreateApplicationServices(Mock.Of<IKaleidoExceptionHandler>()));
        var initializedBeforeNext = false;

        CreateSut().Configure(app => app.Run(_ =>
        {
            initializedBeforeNext = initializer.Invocations.Count == 1;
            return Task.CompletedTask;
        }))(builder);

        await builder.Build()(new DefaultHttpContext
        {
            RequestServices = CreateRequestServices(initializer.Object),
        });

        Assert.True(initializedBeforeNext);
    }

    private static ServiceProvider CreateApplicationServices(IKaleidoExceptionHandler handler)
    {
        var services = new ServiceCollection();
        services.AddSingleton(handler);
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private static ServiceProvider CreateRequestServices(IKaleidoCorrelationContextInitializer initializer)
    {
        var services = new ServiceCollection();
        services.AddSingleton(initializer);
        return services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
