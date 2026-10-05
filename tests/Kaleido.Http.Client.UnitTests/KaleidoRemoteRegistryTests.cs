using Kaleido.Http.Registry;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoRemoteRegistryTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoRemoteRegistry CreateSut(
        IHttpClientFactory httpClientFactory)
    {
        var store = new Mock<IRegistrySnapshotStore>();
        var data = new System.Collections.Concurrent.ConcurrentDictionary<string, AggregatedRegistryResponse>(StringComparer.OrdinalIgnoreCase);
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
        store.Setup(s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) =>
            {
                data.TryRemove(k, out var _);
                return ValueTask.CompletedTask;
            });

        return new KaleidoRemoteRegistry(
            httpClientFactory,
            store.Object,
            NullLogger<KaleidoRemoteRegistry>.Instance);
    }

    private static readonly AggregatedRegistryResponse FakeRegistry = new()
    {
        Processes =
        [
            new Kaleido.Http.Processor.ProcessorRegistryResponse
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
                Kind = Kaleido.Queryable.Metadata.QueryContextKind.Direct
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
            null,
            Mock.Of<ICorrelationHeaderStamper>(),
            CancellationToken.None);

        Assert.Equal("/remote-svc/registry", requestedUrl);
        Assert.Single(result.Processes);
        Assert.Single(result.Queryables);
    }

    [Fact]
    public async Task GetAsync_CachesPerServiceKey()
    {
        var callCount = 0;
        var (sut, _) = CreateSutWithHandler(_ =>
        {
            callCount++;
            return JsonOk(FakeRegistry);
        });

        var stamper = Mock.Of<ICorrelationHeaderStamper>();
        await sut.GetAsync("remote", "remote-svc", null, stamper, CancellationToken.None);
        await sut.GetAsync("remote", "remote-svc", null, stamper, CancellationToken.None);
        // Canonical store key is kaleido:{routePrefix} — another client name
        // pointing at the same service still hits the shared snapshot.
        await sut.GetAsync("alias", "remote-svc", null, stamper, CancellationToken.None);
        // A different service prefix is a different cache entry.
        await sut.GetAsync("other", "other-svc", null, stamper, CancellationToken.None);

        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetAsync_WhenNotFound_Throws()
    {
        var (sut, _) = CreateSutWithHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(() =>
            sut.GetAsync("remote", "remote-svc", null, Mock.Of<ICorrelationHeaderStamper>(), CancellationToken.None));

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
            sut.GetAsync("remote", "remote-svc", null, Mock.Of<ICorrelationHeaderStamper>(), CancellationToken.None));

        Assert.Equal(HttpClientErrorCodes.EmptyResponse, ex.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task GetAsync_DisposesResponse(HttpStatusCode statusCode)
    {
        var content = new DisposalTrackingContent(
            JsonContent.Create(FakeRegistry, options: KaleidoJsonOptions.Options));
        var (sut, _) = CreateSutWithHandler(_ => new HttpResponseMessage(statusCode) { Content = content });

        try
        {
            await sut.GetAsync("remote", "remote-svc", null, Mock.Of<ICorrelationHeaderStamper>(), CancellationToken.None);
        }
        catch (KaleidoHttpClientException) when (statusCode != HttpStatusCode.OK)
        {
        }

        Assert.True(content.Disposed);
    }

    private sealed class DisposalTrackingContent(HttpContent inner) : HttpContent
    {
        public bool Disposed { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) =>
            inner.CopyToAsync(stream);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            inner.Dispose();
            base.Dispose(disposing);
        }
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
        await sut.GetAsync("remote", "remote-svc", null, stamper, CancellationToken.None);

        sut.Invalidate("remote");
        await sut.GetAsync("remote", "remote-svc", null, stamper, CancellationToken.None);

        Assert.Equal(2, callCount);
    }
}
