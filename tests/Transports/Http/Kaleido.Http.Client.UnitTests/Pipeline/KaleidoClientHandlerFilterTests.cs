using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.Pipeline.UnitTests;

public sealed class KaleidoClientHandlerFilterTests : Kaleido.UnitTests.SutFixture
{
    private const string Gateway = "kaleido-gateway";
    private const string Registry = "kaleido-registry";
    private sealed class FirstHandler : DelegatingHandler;
    private sealed class SecondHandler : DelegatingHandler;

    private static KaleidoClientHandlerFilter CreateSut(params Type[] handlers) =>
        new(handlers.Select(x => new KaleidoClientHandlerRegistration(x)), [new KaleidoHttpClientName(Gateway), new KaleidoHttpClientName(Registry)]);

    private static Mock<HttpMessageHandlerBuilder> Builder(string? name)
    {
        var services = new ServiceCollection();
        services.AddTransient<FirstHandler>();
        services.AddTransient<SecondHandler>();
        services.AddKeyedTransient<KaleidoOnBehalfOfGuard>(Gateway);
        services.AddKeyedTransient<KaleidoOnBehalfOfGuard>(Registry);

        var handlers = new List<DelegatingHandler>();
        var builder = new Mock<HttpMessageHandlerBuilder>();
        builder.SetupProperty(x => x.Name, name);
        builder.Setup(x => x.AdditionalHandlers).Returns(handlers);
        builder.Setup(x => x.Services).Returns(services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
        return builder;
    }

    [Fact]
    public void TheGatewayClient_GetsTheHostsHandlers_ThenTheGuard_AfterItsOwnHandlers()
    {
        var builder = Builder(Gateway);
        var configured = new Mock<DelegatingHandler>().Object;

        CreateSut(typeof(FirstHandler), typeof(SecondHandler)).Configure(x => x.AdditionalHandlers.Add(configured))(builder.Object);

        Assert.Collection(builder.Object.AdditionalHandlers,
            x => Assert.Same(configured, x),
            x => Assert.IsType<FirstHandler>(x),
            x => Assert.IsType<SecondHandler>(x),
            x => Assert.IsType<KaleidoOnBehalfOfGuard>(x));
    }

    [Fact]
    public void WithoutHostHandlers_TheGatewayStillGetsTheGuard()
    {
        var builder = Builder(Gateway);

        CreateSut().Configure(_ => { })(builder.Object);

        Assert.IsType<KaleidoOnBehalfOfGuard>(Assert.Single(builder.Object.AdditionalHandlers));
    }

    [Fact]
    public void AHandlerRegisteredTwice_RunsOnce()
    {
        var builder = Builder(Gateway);

        CreateSut(typeof(FirstHandler), typeof(FirstHandler)).Configure(_ => { })(builder.Object);

        Assert.Single(builder.Object.AdditionalHandlers.OfType<FirstHandler>());
    }

    [Theory]
    [InlineData("Intake")]
    [InlineData("PriorAuthEventCollector")]
    [InlineData(null)]
    public void OtherClients_AreLeftAlone(string? name)
    {
        var builder = Builder(name);

        CreateSut(typeof(FirstHandler)).Configure(_ => { })(builder.Object);

        Assert.Empty(builder.Object.AdditionalHandlers);
    }

    [Fact]
    public void TheControlRegistrysClient_GetsTheHostsHandlersAndTheGuard()
    {
        var builder = Builder(Registry);

        CreateSut(typeof(FirstHandler)).Configure(_ => { })(builder.Object);

        Assert.Collection(builder.Object.AdditionalHandlers,
            x => Assert.IsType<FirstHandler>(x),
            x => Assert.IsType<KaleidoOnBehalfOfGuard>(x));
    }
}
