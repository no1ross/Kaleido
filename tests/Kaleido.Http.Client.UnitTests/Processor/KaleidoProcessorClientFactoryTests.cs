using Kaleido.Exceptions;
using Kaleido.Http.Processor;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.Processor.UnitTests;

public sealed class KaleidoProcessorClientFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoProcessorClientFactory CreateSut(
        IHttpClientFactory httpClientFactory,
        KaleidoProcessorClientRouteOptionsMap routeOptionsMap,
        IRegistrySnapshotStore? store = null) =>
        new(
            httpClientFactory,
            Mock.Of<ICorrelationHeaderStamper>(),
            NullLogger<KaleidoProcessorClient>.Instance,
            routeOptionsMap,
            new KaleidoRemoteRegistry(
                httpClientFactory,
                store ?? Mock.Of<IRegistrySnapshotStore>(),
                new KaleidoServiceOptions { ServiceName = "consumer" },
                NullLogger<KaleidoRemoteRegistry>.Instance));

    private static IRegistrySnapshotStore CreateSnapshotStore()
    {
        var data = new System.Collections.Concurrent.ConcurrentDictionary<string, AggregatedRegistryResponse>(StringComparer.OrdinalIgnoreCase);
        var store = new Mock<IRegistrySnapshotStore>();
        store.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) =>
            {
                data.TryGetValue(k, out var snapshot);
                return new ValueTask<AggregatedRegistryResponse?>(snapshot);
            });
        store.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<AggregatedRegistryResponse>(), It.IsAny<CancellationToken>()))
            .Returns((string k, AggregatedRegistryResponse v, CancellationToken _) =>
            {
                data[k] = v;
                return ValueTask.CompletedTask;
            });
        return store.Object;
    }

    [Fact]
    public void GetClient_ForAnUnconfiguredName_FailsNamingTheConfiguredClients()
    {
        var routeMap = new KaleidoProcessorClientRouteOptionsMap();
        routeMap.SetRoutePrefix("Radiology", "radiology");
        routeMap.SetRoutePrefix("History", "history");
        var httpClientFactory = new Mock<IHttpClientFactory>();

        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => CreateSut(httpClientFactory.Object, routeMap).GetClient("Radiolgy"));

        Assert.Equal(HttpClientErrorCodes.UnknownClient, exception.Code);
        Assert.Equal("Kaleido client 'Radiolgy' is not configured under Kaleido:Clients; configured clients: History, Radiology.", exception.Message);
        httpClientFactory.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void GetClient_MatchesTheRegisteredNameCaseInsensitively()
    {
        var routeMap = new KaleidoProcessorClientRouteOptionsMap();
        routeMap.SetRoutePrefix("IntakeService", "intake");
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new System.Net.Http.HttpClient());

        CreateSut(httpClientFactory.Object, routeMap).GetClient("intakeservice");

        httpClientFactory.Verify(f => f.CreateClient("IntakeService"), Times.Once);
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public async Task GetClient_AppliesTheRegisteredRegistryTtl(bool zeroTtl, int expectedFetches)
    {
        var routeMap = new KaleidoProcessorClientRouteOptionsMap();
        routeMap.SetRoutePrefix("Intake", "intake");
        if (zeroTtl)
        {
            routeMap.SetRegistryTtl("Intake", TimeSpan.Zero);
        }

        var fetches = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage _, CancellationToken _) =>
            {
                fetches++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new AggregatedRegistryResponse(), options: KaleidoJsonOptions.Options)
                };
            });
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(() => new System.Net.Http.HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("http://intake/") });

        var client = CreateSut(httpClientFactory.Object, routeMap, CreateSnapshotStore()).GetClient("Intake");
        await client.GetRegistryAsync(TestContext.Current.CancellationToken);
        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expectedFetches, fetches);
    }

    [Fact]
    public void GetClient_WhenNamedClientIsRegistered_ReturnsClient()
    {
        var routeMap = new KaleidoProcessorClientRouteOptionsMap();
        routeMap.SetRoutePrefix("remote-svc", "remote-svc");

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
        routeMap.SetRoutePrefix("IntakeService", "intake");

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
