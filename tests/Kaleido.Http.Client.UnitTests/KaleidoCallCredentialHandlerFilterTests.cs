using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoCallCredentialHandlerFilterTests
    : Kaleido.UnitTests.SutFixture
{
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

    private static KaleidoCallCredentialHandlerFilter CreateSut(params IKaleidoCallCredentialProvider[] providers) =>
        new(Catalog, providers);

    // The pipeline's services, where the filter resolves the client's keyed handler.
    private static Mock<HttpMessageHandlerBuilder> Builder(string? name)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Catalog);
        services.AddSingleton(Mock.Of<IKaleidoCallCredentialProvider>());
        services.AddKeyedTransient<KaleidoCallCredentialHandler>("Intake");
        services.AddKeyedTransient<KaleidoCallCredentialHandler>("Member");

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
    public void WithAProvider_AKaleidoClient_GetsTheCredentialHandler_AfterTheHostsHandlers(string name)
    {
        var builder = Builder(name);
        var hostHandler = new Mock<DelegatingHandler>().Object;

        CreateSut(Mock.Of<IKaleidoCallCredentialProvider>()).Configure(b => b.AdditionalHandlers.Add(hostHandler))(builder.Object);

        Assert.Equal(2, builder.Object.AdditionalHandlers.Count);
        Assert.Same(hostHandler, builder.Object.AdditionalHandlers[0]);
        Assert.IsType<KaleidoCallCredentialHandler>(builder.Object.AdditionalHandlers[1]);
    }

    // Without a provider the host's own handlers decide, as before ADR 0022.
    [Fact]
    public void WithoutAProvider_KaleidoClients_AreLeftAlone()
    {
        var builder = Builder("Intake");

        CreateSut().Configure(_ => { })(builder.Object);

        Assert.Empty(builder.Object.AdditionalHandlers);
    }

    [Theory]
    [InlineData("PriorAuthEventCollector")]
    [InlineData(null)]
    public void OtherClients_AreLeftAlone(string? name)
    {
        var builder = Builder(name);

        CreateSut(Mock.Of<IKaleidoCallCredentialProvider>()).Configure(_ => { })(builder.Object);

        Assert.Empty(builder.Object.AdditionalHandlers);
    }
}
