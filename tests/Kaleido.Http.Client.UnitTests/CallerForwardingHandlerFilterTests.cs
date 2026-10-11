using Kaleido.Http.Processor;
using Kaleido.Http.Queryable;
using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client.UnitTests;

public sealed class CallerForwardingHandlerFilterTests
    : Kaleido.UnitTests.SutFixture
{
    private static CallerForwardingHandlerFilter CreateSut()
    {
        var processMap = new KaleidoProcessorClientRouteOptionsMap();
        processMap.Options["Intake"] = "intake";

        var queryableMap = new KaleidoQueryableClientRouteOptionsMap();
        queryableMap.Options["Member"] = "member";

        return new CallerForwardingHandlerFilter(processMap, queryableMap, Mock.Of<IKaleidoCallerForwarding>());
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
    public void AKaleidoClient_GetsTheForwardingHandler_AfterTheHostsHandlers(string name)
    {
        var builder = Builder(name);
        var hostHandler = new Mock<DelegatingHandler>().Object;

        CreateSut().Configure(b => b.AdditionalHandlers.Add(hostHandler))(builder.Object);

        Assert.Equal(2, builder.Object.AdditionalHandlers.Count);
        Assert.Same(hostHandler, builder.Object.AdditionalHandlers[0]);
        Assert.IsType<CallerForwardingHandler>(builder.Object.AdditionalHandlers[1]);
    }

    [Theory]
    [InlineData("PriorAuthEventCollector")]
    [InlineData(null)]
    public void OtherClients_AreLeftAlone(string? name)
    {
        var builder = Builder(name);

        CreateSut().Configure(_ => { })(builder.Object);

        Assert.Empty(builder.Object.AdditionalHandlers);
    }
}
