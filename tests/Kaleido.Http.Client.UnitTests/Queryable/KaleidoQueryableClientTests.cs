using Kaleido.Http.Client;
using Kaleido.Http.Client.Queryable;
using Kaleido.Http.Queryable.Contracts;
using Kaleido.Observability;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Queryable.Http.Client.Tests;

public sealed class KaleidoQueryableClientTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static readonly QueryableRecordResponse FakeContext = new()
    {
        Name = "my-context",
        DisplayName = "My Context",
        Description = "Test context.",
        Version = "1.0.0",
        Source = "test",
        Kind = QueryContextKind.Direct,
        MetadataUrl = "/queryable/my-context/metadata",
        QueryUrl = "/queryable/my-context/query",
        Fields = [],
        Views =
        [
            new QueryableViewResponse
            {
                Name = "grid",
                DisplayName = "Grid",
                Description = "Grid view.",
                Version = "1.0.0",
                Visibility = QueryViewVisibility.Public,
                QueryUrl = "/queryable/my-context/grid/query",
                Parameters = [],
                OutputFields = [],
                Pageable = null
            }
        ]
    };

    private static HttpResponseMessage JsonOk<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };

    private static Mock<HttpMessageHandler> HandlerThatReturns(
        Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) => respond(req));
        return mock;
    }

    private static (KaleidoQueryableClient client, Mock<HttpMessageHandler> handler) CreateClient(
        string routePrefix = "",
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var registry = new[] { FakeContext };
        var handler = HandlerThatReturns(respond ?? (_ => JsonOk(registry)));
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };

        var stamper = new Mock<ICorrelationHeaderStamper>();

        var client = new KaleidoQueryableClient(httpClient, stamper.Object, NullLogger<KaleidoQueryableClient>.Instance, routePrefix);
        return (client, handler);
    }

    // ---------------------------------------------------------------------------
    // GetRegistryAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_ReturnsRegistry()
    {
        var (client, _) = CreateClient();

        var result = await client.GetRegistryAsync();

        Assert.Single(result, r => r.Name == "my-context");
    }

    [Fact]
    public async Task GetRegistryAsync_CachesRegistry_DoesNotSendSecondRequest()
    {
        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callCount++;
            return JsonOk(new[] { FakeContext });
        });

        await client.GetRegistryAsync();
        await client.GetRegistryAsync();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetRegistryAsync_WhenResponseBodyIsNull_Throws()
    {
        var handler = HandlerThatReturns(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null")
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") }
            }
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = new KaleidoQueryableClient(httpClient, new Mock<ICorrelationHeaderStamper>().Object, NullLogger<KaleidoQueryableClient>.Instance);

        await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.GetRegistryAsync());
    }

    // ---------------------------------------------------------------------------
    // GetContextMetadataAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetContextMetadataAsync_ResolvesContextAndFetchesMetadataUrl()
    {
        var callUrls = new List<string>();

        var (client, _) = CreateClient(respond: req =>
        {
            callUrls.Add(req.RequestUri!.PathAndQuery);
            return req.RequestUri!.PathAndQuery.Contains("metadata")
                ? JsonOk(FakeContext)
                : JsonOk(new[] { FakeContext });
        });

        var result = await client.GetContextMetadataAsync("my-context");

        Assert.Equal("my-context", result.Name);
        Assert.Contains(callUrls, u => u.Contains("metadata"));
    }

    [Fact]
    public async Task GetContextMetadataAsync_WhenContextNotFound_Throws()
    {
        var (client, _) = CreateClient();

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.GetContextMetadataAsync("does-not-exist"));

        Assert.Contains("does-not-exist", ex.Message);
    }

    [Fact]
    public async Task GetContextMetadataAsync_WhenMetadataHttpFails_ThrowsKaleidoException()
    {
        var callCount = 0;
        var handler = HandlerThatReturns(req =>
        {
            callCount++;
            // First call = registry, second call = metadata
            if (callCount == 1)
            {
                return JsonOk(new[] { FakeContext });
            }

            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = new KaleidoQueryableClient(httpClient, new Mock<ICorrelationHeaderStamper>().Object, NullLogger<KaleidoQueryableClient>.Instance);

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.GetContextMetadataAsync("my-context"));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
        Assert.Equal(HttpClientErrorCodes.RequestFailed, ex.Code);
    }

    // ---------------------------------------------------------------------------
    // QueryViewAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryViewAsync_PostsToViewQueryUrl_AndReturnsResult()
    {
        var expectedResult = new QueryResult<FakeView>(1, 0, 1, [new FakeView { Id = 42 }]);
        string? postedUrl = null;

        var callCount = 0;
        var handler = HandlerThatReturns(req =>
        {
            callCount++;
            if (callCount == 1)
            {
                return JsonOk(new[] { FakeContext });
            }

            postedUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(expectedResult);
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = new KaleidoQueryableClient(httpClient, new Mock<ICorrelationHeaderStamper>().Object, NullLogger<KaleidoQueryableClient>.Instance);

        var result = await client.QueryViewAsync<FakeParams, FakeView>(
            "my-context", "grid",
            new QueryApiRequest<FakeParams>(new FakeParams(), new QueryBody()));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(42, result.Results.First().Id);
        Assert.Contains("grid", postedUrl);
    }

    [Fact]
    public async Task QueryViewAsync_WhenContextNotFound_Throws()
    {
        var (client, _) = CreateClient();

        await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QueryViewAsync<FakeParams, FakeView>(
                "no-such-context", "grid",
                new QueryApiRequest<FakeParams>(new FakeParams(), new QueryBody())));
    }

    [Fact]
    public async Task QueryViewAsync_WhenViewNotFound_Throws()
    {
        var (client, _) = CreateClient();

        await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QueryViewAsync<FakeParams, FakeView>(
                "my-context", "no-such-view",
                new QueryApiRequest<FakeParams>(new FakeParams(), new QueryBody())));
    }

    [Fact]
    public async Task QueryViewAsync_WhenHttpFails_ThrowsKaleidoException()
    {
        var callCount = 0;
        var handler = HandlerThatReturns(req =>
        {
            callCount++;
            if (callCount == 1)
            {
                return JsonOk(new[] { FakeContext });
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = new KaleidoQueryableClient(httpClient, new Mock<ICorrelationHeaderStamper>().Object, NullLogger<KaleidoQueryableClient>.Instance);

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QueryViewAsync<FakeParams, FakeView>(
                "my-context", "grid",
                new QueryApiRequest<FakeParams>(new FakeParams(), new QueryBody())));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // QueryContextAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryContextAsync_PostsToContextQueryUrl_AndReturnsResult()
    {
        var expectedResult = new QueryResult<FakeView>(2, 0, 2, [new FakeView { Id = 1 }, new FakeView { Id = 2 }]);

        var callCount = 0;
        var handler = HandlerThatReturns(req =>
        {
            callCount++;
            if (callCount == 1)
            {
                return JsonOk(new[] { FakeContext });
            }

            return JsonOk(expectedResult);
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = new KaleidoQueryableClient(httpClient, new Mock<ICorrelationHeaderStamper>().Object, NullLogger<KaleidoQueryableClient>.Instance);

        var result = await client.QueryContextAsync<FakeView>(
            "my-context",
            new QueryApiRequest(new QueryBody()));

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task QueryContextAsync_WhenContextHasNoQueryUrl_Throws()
    {
        var noQueryContext = FakeContext with { QueryUrl = null };
        var (client, _) = CreateClient(respond: _ => JsonOk(new[] { noQueryContext }));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QueryContextAsync<FakeView>(
                "my-context",
                new QueryApiRequest(new QueryBody())));

        Assert.Contains("direct", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------
    // Route prefix
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task RoutePrefix_IsAppliedToRegistryUrl()
    {
        string? registryUrl = null;
        var (client, _) = CreateClient(routePrefix: "radiology", respond: req =>
        {
            registryUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(new[] { FakeContext });
        });

        await client.GetRegistryAsync();

        Assert.Equal("/radiology/queryable/registry", registryUrl);
    }

    [Fact]
    public async Task RoutePrefix_WhenEmpty_UsesDefaultRegistryUrl()
    {
        string? registryUrl = null;
        var (client, _) = CreateClient(routePrefix: "", respond: req =>
        {
            registryUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(new[] { FakeContext });
        });

        await client.GetRegistryAsync();

        Assert.Equal("/queryable/registry", registryUrl);
    }

    // ---------------------------------------------------------------------------
    // Correlation headers
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryContextAsync_StampsCorrelationHeadersOnRequest()
    {
        var expectedResult = new QueryResult<FakeView>(0, 0, 0, []);
        var callCount = 0;

        var handler = HandlerThatReturns(req =>
        {
            callCount++;
            return callCount == 1
                ? JsonOk(new[] { FakeContext })
                : JsonOk(expectedResult);
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var stamper = new Mock<ICorrelationHeaderStamper>();

        var client = new KaleidoQueryableClient(httpClient, stamper.Object, NullLogger<KaleidoQueryableClient>.Instance);
        await client.QueryContextAsync<FakeView>("my-context", new QueryApiRequest(new QueryBody()));

        stamper.Verify(x => x.Stamp(It.IsAny<HttpRequestMessage>()), Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------------------
    // Fake types
    // ---------------------------------------------------------------------------

    private sealed class FakeParams { }
    private sealed class FakeView { public int Id { get; init; } }
}
