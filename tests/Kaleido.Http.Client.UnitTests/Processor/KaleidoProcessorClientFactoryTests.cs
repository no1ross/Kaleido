using Kaleido.Http.Processor;
using Kaleido.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.Processor.UnitTests;

public sealed class KaleidoProcessorClientFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoProcessorClientFactory CreateSut(
        IHttpClientFactory httpClientFactory,
        KaleidoProcessorClientRouteOptionsMap routeOptionsMap) =>
        new(
            httpClientFactory,
            Mock.Of<ICorrelationHeaderStamper>(),
            NullLogger<KaleidoProcessorClient>.Instance,
            routeOptionsMap,
            new KaleidoRemoteRegistry(
                httpClientFactory,
                Mock.Of<IRegistrySnapshotStore>(),
                NullLogger<KaleidoRemoteRegistry>.Instance));
    [Fact]
    public void GetClient_WhenNamedClientIsRegistered_ReturnsClient()
    {
        var routeMap = new KaleidoProcessorClientRouteOptionsMap();
        routeMap.Options["remote-svc"] = "remote-svc";

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new System.Net.Http.HttpClient());

        var factory = CreateSut(httpClientFactory.Object, routeMap);

        var client = factory.GetClient("remote-svc");

        Assert.NotNull(client);
    }

    [Fact]
    public void TryGetClientForService_FindsTheClientByItsRoutePrefix()
    {
        var routeMap = new KaleidoProcessorClientRouteOptionsMap();
        routeMap.Options["IntakeService"] = "intake";

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new System.Net.Http.HttpClient());

        var factory = CreateSut(httpClientFactory.Object, routeMap);

        Assert.True(factory.TryGetClientForService("Intake", out var client));
        Assert.NotNull(client);
        httpClientFactory.Verify(f => f.CreateClient("IntakeService"));
    }

    [Fact]
    public void TryGetClientForService_ForAServiceWithoutAClient_ReturnsFalse()
    {
        var factory = CreateSut(Mock.Of<IHttpClientFactory>(), new KaleidoProcessorClientRouteOptionsMap());

        Assert.False(factory.TryGetClientForService("radiology", out var client));
        Assert.Null(client);
    }
}