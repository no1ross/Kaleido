using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoCallCredentialHandlerFilterTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoCallCredentialHandlerFilter CreateSut(params IKaleidoCallCredentialProvider[] providers)
    {
        var processMap = new KaleidoProcessorClientRouteOptionsMap();
        processMap.SetRoutePrefix("Intake", "intake");

        var queryableMap = new KaleidoQueryableClientRouteOptionsMap();
        queryableMap.SetRoutePrefix("Member", "member");

        return new KaleidoCallCredentialHandlerFilter(processMap, queryableMap, providers);
    }

    private static Mock<HttpMessageHandlerBuilder> Builder(string? name)
    {
        var handlers = new List<DelegatingHandler>();
        var builder = new Mock<HttpMessageHandlerBuilder>();
        builder.SetupProperty(x => x.Name, name);
        builder.Setup(x => x.AdditionalHandlers).Returns(handlers);
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
