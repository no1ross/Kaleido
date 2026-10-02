using Kaleido.Http.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoRemoteRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoRemoteRegistry CreateSut(
        IHttpClientFactory httpClientFactory) =>
        new(
            httpClientFactory,
            NullLogger<KaleidoRemoteRegistry>.Instance);

    private static readonly AggregatedRegistryResponse FakeRegistry = new()
    {
        Processes =
        [
            new Kaleido.Http.Process.ProcessorRegistryResponse
            {
                ServiceName = "remote-svc",
                Name = "remote-svc"
            }
        ],
        Queryables =
        [
            new Kaleido.Http.Queryable.QueryableRecordResponse
            {
                ServiceName = "remote-svc",
                Name = "my-context",
                Kind = Kaleido.Queryable.Metadata.QueryContextKind.Direct,
                MetadataUrl = "/remote-svc/queryable/my-context/metadata"
            }
        ]
    };

    private static (KaleidoRemoteRegistry sut, Mock<HttpMessageHandler> handler) CreateSutWithHandler(
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                respond?.Invoke(req) ?? JsonOk(FakeRegistry));

        var httpClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        return (CreateSut(factory.Object), handler);
    }

    private static HttpResponseMessage JsonOk<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value, options: KaleidoJsonOptions.Options)
        };

    [Fact]
    public async Task GetAsync_FetchesUnifiedRegistryEndpoint()
    {
        string? requestedUrl = null;
        var (sut, _) = CreateSutWithHandler(req =>
        {
            requestedUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(FakeRegistry);
        });

        var result = await sut.GetAsync(
            "remote",
            "remote-svc",
            Mock.Of<ICorrelationHeaderStamper>(),
            CancellationToken.None);

        Assert.Equal("/remote-svc/registry", requestedUrl);
        Assert.Single(result.Processes);
        Assert.Single(result.Queryables);
    }

    [Fact]
    public async Task GetAsync_CachesPerClientName()
    {
        var callCount = 0;
        var (sut, _) = CreateSutWithHandler(_ =>
        {
            callCount++;
            return JsonOk(FakeRegistry);
        });

        var stamper = Mock.Of<ICorrelationHeaderStamper>();
        await sut.GetAsync("remote", "remote-svc", stamper, CancellationToken.None);
        await sut.GetAsync("remote", "remote-svc", stamper, CancellationToken.None);
        await sut.GetAsync("other", "remote-svc", stamper, CancellationToken.None);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetAsync_WhenNotFound_Throws()
    {
        var (sut, _) = CreateSutWithHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(() =>
            sut.GetAsync("remote", "remote-svc", Mock.Of<ICorrelationHeaderStamper>(), CancellationToken.None));

        Assert.Equal(HttpClientErrorCodes.RequestFailed, ex.Code);
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task GetAsync_WhenResponseBodyIsNull_Throws()
    {
        var (sut, _) = CreateSutWithHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null")
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") }
            }
        });

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(() =>
            sut.GetAsync("remote", "remote-svc", Mock.Of<ICorrelationHeaderStamper>(), CancellationToken.None));

        Assert.Equal(HttpClientErrorCodes.EmptyResponse, ex.Code);
    }

    [Fact]
    public async Task Invalidate_RefetchesClient()
    {
        var callCount = 0;
        var (sut, _) = CreateSutWithHandler(_ =>
        {
            callCount++;
            return JsonOk(FakeRegistry);
        });

        var stamper = Mock.Of<ICorrelationHeaderStamper>();
        await sut.GetAsync("remote", "remote-svc", stamper, CancellationToken.None);

        sut.Invalidate("remote");
        await sut.GetAsync("remote", "remote-svc", stamper, CancellationToken.None);

        Assert.Equal(2, callCount);
    }
}
