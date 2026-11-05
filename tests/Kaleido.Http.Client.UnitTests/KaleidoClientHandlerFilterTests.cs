using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoClientHandlerFilterTests
    : Kaleido.UnitTests.SutFixture
{
    private sealed class FirstHandler : DelegatingHandler;

    private sealed class SecondHandler : DelegatingHandler;

    private static KaleidoClientDefinition Client(string name) =>
        new(name, name.ToLowerInvariant(), RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));

    private static readonly IKaleidoClientCatalog Catalog = CreateCatalog();

    private static IKaleidoClientCatalog CreateCatalog()
    {
        var catalog = new Mock<IKaleidoClientCatalog>();
        catalog.Setup(x => x.Find(It.IsAny<string>())).Returns((string name) =>
            new[] { Client("Intake"), Client("Member") }.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)));
        return catalog.Object;
    }

    private static KaleidoClientHandlerFilter CreateSut(params Type[] handlers) =>
        new(Catalog, handlers.Select(x => new KaleidoClientHandlerRegistration(x)));

    // The pipeline's services, where the filter resolves the host's handlers and the client's guard.
    private static Mock<HttpMessageHandlerBuilder> Builder(string? name)
    {
        var services = new ServiceCollection();
        services.AddTransient<FirstHandler>();
        services.AddTransient<SecondHandler>();

        foreach (var client in new[] { "Intake", "intake", "Member", CentralRegistrySource.ClientName })
        {
            services.AddKeyedTransient<KaleidoOnBehalfOfGuard>(client);
        }

        var handlers = new List<DelegatingHandler>();
        var builder = new Mock<HttpMessageHandlerBuilder>();
        builder.SetupProperty(x => x.Name, name);
        builder.Setup(x => x.AdditionalHandlers).Returns(handlers);
        builder.Setup(x => x.Services).Returns(services.BuildServiceProvider());
        return builder;
    }

    [Theory]
    [InlineData("Intake")]
    [InlineData("intake")]
    [InlineData("Member")]
    public void AKaleidoClient_GetsTheHostsHandlers_ThenTheGuard_AfterItsOwnHandlers(string name)
    {
        var builder = Builder(name);
        var configured = new Mock<DelegatingHandler>().Object;

        CreateSut(typeof(FirstHandler), typeof(SecondHandler)).Configure(b => b.AdditionalHandlers.Add(configured))(builder.Object);

        Assert.Collection(
            builder.Object.AdditionalHandlers,
            x => Assert.Same(configured, x),
            x => Assert.IsType<FirstHandler>(x),
            x => Assert.IsType<SecondHandler>(x),
            x => Assert.IsType<KaleidoOnBehalfOfGuard>(x));
    }

    // Without host handlers the guard is still there: Forward is the default, every caller's call is guarded.
    [Fact]
    public void WithoutHostHandlers_AKaleidoClient_StillGetsTheGuard()
    {
        var builder = Builder("Intake");

        CreateSut().Configure(_ => { })(builder.Object);

        Assert.IsType<KaleidoOnBehalfOfGuard>(Assert.Single(builder.Object.AdditionalHandlers));
    }

    [Fact]
    public void AHandlerRegisteredTwice_RunsOnce()
    {
        var builder = Builder("Intake");

        CreateSut(typeof(FirstHandler), typeof(FirstHandler)).Configure(_ => { })(builder.Object);

        Assert.Single(builder.Object.AdditionalHandlers.OfType<FirstHandler>());
    }

    [Theory]
    [InlineData("PriorAuthEventCollector")]
    [InlineData(null)]
    public void OtherClients_AreLeftAlone(string? name)
    {
        var builder = Builder(name);

        CreateSut(typeof(FirstHandler)).Configure(_ => { })(builder.Object);

        Assert.Empty(builder.Object.AdditionalHandlers);
    }

    // #553: the control registry's client is a Kaleido client too, though it is not in the catalog.
    [Fact]
    public void TheControlRegistrysClient_GetsTheHostsHandlersAndTheGuard()
    {
        var builder = Builder(CentralRegistrySource.ClientName);

        CreateSut(typeof(FirstHandler)).Configure(_ => { })(builder.Object);

        Assert.Collection(
            builder.Object.AdditionalHandlers,
            x => Assert.IsType<FirstHandler>(x),
            x => Assert.IsType<KaleidoOnBehalfOfGuard>(x));
    }
}
