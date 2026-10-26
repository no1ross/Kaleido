using Kaleido.Http.Queryable;
using Kaleido.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.Queryable.UnitTests;

public sealed class KaleidoQueryableClientTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoQueryableClient CreateSut(
        HttpClient httpClient,
        ICorrelationHeaderStamper headerStamper,
        string callerServiceName = "",
        ILogger<KaleidoQueryableClient>? logger = null)
    {
        // Registry fetches go through the shared remote registry, which pulls
        // the HttpClient from IHttpClientFactory — return the test client.
        var factory = new Mock<IHttpClientFactory>();
        factory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        var remoteRegistry = new KaleidoRemoteRegistry(
            factory.Object,
            CreateSnapshotStore(),
            new KaleidoServiceOptions { ServiceName = "consumer" },
            NullLogger<KaleidoRemoteRegistry>.Instance);

        return new(
            httpClient,
            headerStamper,
            logger ?? NullLogger<KaleidoQueryableClient>.Instance,
            remoteRegistry,
            "test",
            callerServiceName);
    }

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
        store.Setup(s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) =>
            {
                data.TryRemove(k, out var _);
                return ValueTask.CompletedTask;
            });
        return store.Object;
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static readonly QueryableSourceResponse FakeContext = new()
    {
        ServiceName = "test-svc",
        Name = "my-context",
        DisplayName = "My Context",
        Description = "Test context.",
        Source = "test",
        QueryUrl = "/queryable/my-context/query",
        Fields = [],
        Views =
        [
            new QueryableViewResponse
            {
                Name = "grid",
                DisplayName = "Grid",
                Description = "Grid view.",
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
            Content = JsonContent.Create(value, options: KaleidoJsonOptions.Options)
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

    private static readonly AggregatedRegistryResponse FakeRegistry = new()
    {
        Queryables = [FakeContext]
    };

    private static (KaleidoQueryableClient client, Mock<HttpMessageHandler> handler) CreateClient(
        string routePrefix = "",
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var handler = HandlerThatReturns(respond ?? (_ => JsonOk(FakeRegistry)));
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };

        var stamper = new Mock<ICorrelationHeaderStamper>();

        var client = CreateSut(httpClient, stamper.Object, routePrefix);
        return (client, handler);
    }

    // ---------------------------------------------------------------------------
    // GetRegistryAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_ReturnsRegistry()
    {
        var (client, _) = CreateClient();

        var result = await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Single(result, r => r.Name == "my-context");
    }

    [Fact]
    public async Task GetRegistryAsync_CachesRegistry_DoesNotSendSecondRequest()
    {
        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callCount++;
            return JsonOk(FakeRegistry);
        });

        await client.GetRegistryAsync(TestContext.Current.CancellationToken);
        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task InvalidateRegistryAsync_DropsTheSnapshot_SoTheNextReadRefetches()
    {
        var callCount = 0;
        var (client, _) = CreateClient(respond: _ =>
        {
            callCount++;
            return JsonOk(FakeRegistry);
        });

        await client.GetRegistryAsync(TestContext.Current.CancellationToken);
        await client.InvalidateRegistryAsync(TestContext.Current.CancellationToken);
        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, callCount);
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
        var client = CreateSut(httpClient, new Mock<ICorrelationHeaderStamper>().Object);

        await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.GetRegistryAsync(TestContext.Current.CancellationToken));
    }

    // ---------------------------------------------------------------------------
    // QueryViewAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QueryViewAsync_PostsToViewQueryUrl_AndReturnsResult()
    {
        var expectedResult = new QueryResult<FakeView>(1, 0, 1, [new FakeView { Id = 42 }]);
        string? postedUrl = null;

        var handler = HandlerThatReturns(req =>
        {
            postedUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(expectedResult);
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = CreateSut(httpClient, new Mock<ICorrelationHeaderStamper>().Object);

        var result = await client.QueryViewAsync<FakeParams, FakeView>(
            "my-context", "grid",
            new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody()), TestContext.Current.CancellationToken);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(42, result.Results.First().Id);
        Assert.Equal("/queryable/my-context/grid/query", postedUrl);
    }

    // An unknown source or view (or one the caller may not use) is the service's own answer.
    [Theory]
    [InlineData("no-such-context", "grid", HttpStatusCode.NotFound)]
    [InlineData("my-context", "no-such-view", HttpStatusCode.NotFound)]
    [InlineData("my-context", "grid", HttpStatusCode.Forbidden)]
    public async Task QueryViewAsync_WhenTheServiceRefuses_ThrowsWithItsStatus(string source, string view, HttpStatusCode status)
    {
        var (client, _) = CreateClient(respond: _ => new HttpResponseMessage(status));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QueryViewAsync<FakeParams, FakeView>(
                source, view,
                new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody()), TestContext.Current.CancellationToken));

        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(HttpClientErrorCodes.RequestFailed, ex.Code);
    }

    [Fact]
    public async Task QueryViewAsync_WhenHttpFails_ThrowsKaleidoException()
    {
        var handler = HandlerThatReturns(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = CreateSut(httpClient, new Mock<ICorrelationHeaderStamper>().Object);

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QueryViewAsync<FakeParams, FakeView>(
                "my-context", "grid",
                new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody()), TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // QuerySourceAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QuerySourceAsync_PostsToSourceQueryUrl_AndReturnsResult()
    {
        var expectedResult = new QueryResult<FakeView>(2, 0, 2, [new FakeView { Id = 1 }, new FakeView { Id = 2 }]);

        var handler = HandlerThatReturns(_ => JsonOk(expectedResult));
        using var httpClient = new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = CreateSut(httpClient, new Mock<ICorrelationHeaderStamper>().Object);

        var result = await client.QuerySourceAsync<FakeView>(
            "my-context",
            new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task QuerySourceAsync_WithParameters_PostsParametersToSourceQueryUrl()
    {
        string? postedUrl = null;
        string? postedBody = null;
        var handler = HandlerThatReturns(req =>
        {
            postedUrl = req.RequestUri!.PathAndQuery;
            postedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonOk(new QueryResult<FakeView>(1, 0, 1, [new FakeView { Id = 7 }]));
        });
        using var httpClient = new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("http://localhost") };
        var client = CreateSut(httpClient, new Mock<ICorrelationHeaderStamper>().Object);

        var result = await client.QuerySourceAsync<FakeSearchParams, FakeView>(
            "my-context",
            new QueryApiRequest<FakeSearchParams>(new FakeSearchParams { ProcessId = "p-1" }, new QueryApiBody()), TestContext.Current.CancellationToken);

        Assert.Equal("/queryable/my-context/query", postedUrl);
        Assert.Contains("p-1", postedBody);
        Assert.Equal(7, Assert.Single(result.Results).Id);
    }

    // A source the caller may only use through views is refused by the service (403), whatever a
    // cached registry copy says.
    [Fact]
    public async Task QuerySourceAsync_WhenTheCallerMayNotQueryTheSource_ThrowsWithTheServicesStatus()
    {
        var (client, _) = CreateClient(respond: _ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QuerySourceAsync<FakeView>(
                "my-context",
                new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
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
            return JsonOk(FakeRegistry);
        });

        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/radiology/registry", registryUrl);
    }

    [Fact]
    public async Task RoutePrefix_WhenEmpty_UsesDefaultRegistryUrl()
    {
        string? registryUrl = null;
        var (client, _) = CreateClient(routePrefix: "", respond: req =>
        {
            registryUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(FakeRegistry);
        });

        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal("/registry", registryUrl);
    }

    // ---------------------------------------------------------------------------
    // Correlation headers
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task QuerySourceAsync_StampsCorrelationHeadersOnRequest()
    {
        var expectedResult = new QueryResult<FakeView>(0, 0, 0, []);
        var callCount = 0;

        var handler = HandlerThatReturns(req =>
        {
            callCount++;
            return callCount == 1
                ? JsonOk(FakeRegistry)
                : JsonOk(expectedResult);
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var stamper = new Mock<ICorrelationHeaderStamper>();

        var client = CreateSut(httpClient, stamper.Object);
        await client.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken);

        stamper.Verify(x => x.Stamp(It.IsAny<HttpRequestMessage>()), Times.AtLeastOnce);
    }

    // ---------------------------------------------------------------------------
    // Failures and logging
    // ---------------------------------------------------------------------------

    private static (KaleidoQueryableClient Client, RecordingLogger Logger, List<string> Queries) CreateQueryClient(
        Func<HttpResponseMessage> queryResponse,
        AggregatedRegistryResponse? registry = null)
    {
        var queries = new List<string>();
        var handler = HandlerThatReturns(req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal))
            {
                return JsonOk(registry ?? FakeRegistry);
            }

            queries.Add(req.RequestUri.ToString());
            return queryResponse();
        });
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var logger = new RecordingLogger();

        return (CreateSut(httpClient, Mock.Of<ICorrelationHeaderStamper>(), "caller", logger), logger, queries);
    }

    private static HttpResponseMessage JsonNull(HttpStatusCode statusCode) =>
        new(statusCode)
        {
            Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task QuerySourceAsync_WhenTheServiceRejectsTheQuery_ThrowsValidationFailedWithTheErrors()
    {
        var (client, _, _) = CreateQueryClient(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(
                new KaleidoErrorResponse([new KaleidoError("qry_invalid_filter", "Unknown field 'x'.")]),
                options: KaleidoJsonOptions.Options),
        });

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken));

        Assert.Equal(HttpClientErrorCodes.ValidationFailed, exception.Code);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("qry_invalid_filter", Assert.Single(exception.Errors).Code);
        Assert.Contains("source 'my-context'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Unknown field 'x'.", exception.Message, StringComparison.Ordinal);
    }

    // #444: an error body that is not JSON (a proxy's HTML page) is a KaleidoHttpClientException, never a
    // JsonException; and a conflict is reported as one, as on the processor client.
    [Fact]
    public async Task QuerySourceAsync_ABadRequestWithAnHtmlBody_ThrowsRequestFailed_NotAJsonException()
    {
        var (client, _, _) = CreateQueryClient(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("<html>Bad request</html>", System.Text.Encoding.UTF8, "text/html"),
        });

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken));

        Assert.Equal(HttpClientErrorCodes.RequestFailed, exception.Code);
    }

    [Fact]
    public async Task QuerySourceAsync_AForbiddenWithErrors_KeepsTheServicesCodes()
    {
        var (client, _, _) = CreateQueryClient(() => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = JsonContent.Create(new KaleidoErrorResponse([new KaleidoError("authorization_denied", "Not allowed.")]), options: KaleidoJsonOptions.Options),
        });

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken));

        Assert.Equal(HttpClientErrorCodes.RequestFailed, exception.Code);
        Assert.Equal("authorization_denied", Assert.Single(exception.Errors).Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QuerySourceAsync_ABadRequestWithoutStructuredErrors_ThrowsRequestFailed(bool nullBody)
    {
        var (client, _, _) = CreateQueryClient(() => nullBody
            ? JsonNull(HttpStatusCode.BadRequest)
            : new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(new KaleidoErrorResponse([]), options: KaleidoJsonOptions.Options),
            });

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken));

        Assert.Equal(HttpClientErrorCodes.RequestFailed, exception.Code);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("status code 400 (BadRequest)", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryViewAsync_WhenTheServiceReturnsNoPayload_ThrowsEmptyResponse()
    {
        var (client, _, _) = CreateQueryClient(() => JsonNull(HttpStatusCode.OK));

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QueryViewAsync<FakeParams, FakeView>(
                "my-context",
                "grid",
                new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody()),
                TestContext.Current.CancellationToken));

        Assert.Equal(HttpClientErrorCodes.EmptyResponse, exception.Code);
        Assert.Contains("view 'grid' on source 'my-context'", exception.Message, StringComparison.Ordinal);
    }

    // Query URLs come from the route prefix, never from the registry (#381), so a registry URL
    // pointing elsewhere is never followed.
    [Fact]
    public async Task QuerySourceAsync_IgnoresTheRegistryQueryUrl_AndPostsToTheContractUrl()
    {
        var registry = new AggregatedRegistryResponse { Queryables = [FakeContext with { QueryUrl = "http://evil.example/steal" }] };
        var (client, _, queries) = CreateQueryClient(() => JsonOk(new QueryResult<FakeView>(0, 0, 0, [])), registry);

        await client.QuerySourceAsync<FakeView>("My-Context", new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken);
        await client.QueryViewAsync<FakeParams, FakeView>("my-context", "Grid", new QueryApiRequest<FakeParams>(new FakeParams(), new QueryApiBody()), TestContext.Current.CancellationToken);

        Assert.Equal(["http://localhost/caller/queryable/my-context/query", "http://localhost/caller/queryable/my-context/grid/query"], queries);
    }

    [Fact]
    public async Task QuerySourceAsync_LogsTheOutgoingQueryAtDebug()
    {
        var (client, logger, _) = CreateQueryClient(() => JsonOk(new QueryResult<FakeView>(0, 0, 0, [])));

        await client.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("Sending POST", entry.Message, StringComparison.Ordinal);
        Assert.Contains("/queryable/my-context/query", entry.Message, StringComparison.Ordinal);
        Assert.Contains("remote queryable 'caller'", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuerySourceAsync_WhenTheServiceFails_LogsAWarningWithTheStatus()
    {
        var (client, logger, _) = CreateQueryClient(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(new QueryApiBody()), TestContext.Current.CancellationToken));

        Assert.Collection(
            logger.Entries,
            sending => Assert.Equal(LogLevel.Debug, sending.Level),
            failed =>
            {
                Assert.Equal(LogLevel.Warning, failed.Level);
                Assert.Contains("Remote queryable 'caller' returned 503 (ServiceUnavailable) for POST", failed.Message, StringComparison.Ordinal);
                Assert.Contains("/queryable/my-context/query", failed.Message, StringComparison.Ordinal);
            });
    }

    private sealed class RecordingLogger : ILogger<KaleidoQueryableClient>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    // ---------------------------------------------------------------------------
    // Fake types
    // ---------------------------------------------------------------------------

    private sealed class FakeParams { }
    private sealed class FakeSearchParams { public string ProcessId { get; init; } = string.Empty; }
    private sealed class FakeView { public int Id { get; init; } }

    // A 404 from a query means the service was deployed without that source: the registry is loaded again.
    [Fact]
    public async Task QuerySourceAsync_WhenTheServiceAnswers404_DropsTheRegistrySnapshot()
    {
        var registryReads = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal))
            {
                registryReads++;
                return JsonOk(FakeRegistry);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await client.GetRegistryAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<KaleidoHttpClientException>(() =>
            client.QuerySourceAsync<FakeView>("my-context", new QueryApiRequest(), TestContext.Current.CancellationToken));
        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, registryReads);
    }
}
