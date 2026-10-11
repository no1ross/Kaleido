using Kaleido.Http.Processor;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Client.Processor.UnitTests;

public sealed class KaleidoProcessorClientTests
    : Kaleido.UnitTests.SutFixture
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static KaleidoProcessorClient CreateSut(
        HttpClient httpClient,
        ICorrelationHeaderStamper headerStamper,
        string serviceName = "")
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
            NullLogger<KaleidoRemoteRegistry>.Instance);

        return new(
            httpClient,
            headerStamper,
            NullLogger<KaleidoProcessorClient>.Instance,
            remoteRegistry,
            "test",
            serviceName);
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
        return store.Object;
    }

    private static readonly ProcessStepResponse FakeStep = new()
    {
        Name = "MyStep",
        Description = "Test step.",
        Version = "1.0",
        ExecuteUrl = "/processes/steps/mystep",
        Fields = [],
        Dependencies = [],
        AvailableAfter = [],
        AvailableUntil = []
    };

    private static readonly ProcessorRegistryResponse FakeProcessor = new()
    {
        ServiceName = "test-processor",
        Name = "test-processor",
        Description = "Test processor.",
        DisplayName = "Test Processor",
        RegistryUrl = "/test-processor/registry",
        Steps = [FakeStep],
        InitialSteps = []
    };

    private static readonly AggregatedRegistryResponse FakeRegistry = new()
    {
        Processes = [FakeProcessor]
    };

    private static HttpResponseMessage JsonOk<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value, options: KaleidoJsonOptions.Options)
        };

    private static (KaleidoProcessorClient client, Mock<HttpMessageHandler> handler) CreateClient(
        string routePrefix = "",
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                respond != null ? respond(req) : JsonOk(FakeRegistry));

        var httpClient = new HttpClient(mock.Object) { BaseAddress = new Uri("http://localhost") };
        var stamper = new Mock<ICorrelationHeaderStamper>();

        var client = CreateSut(httpClient, stamper.Object, routePrefix);
        return (client, mock);
    }

    // ---------------------------------------------------------------------------
    // GetRegistryAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_ReturnsRegistry()
    {
        var (client, _) = CreateClient();

        var result = await client.GetRegistryAsync();

        Assert.Single(result, p => p.Name == "test-processor");
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

        await client.GetRegistryAsync();
        await client.GetRegistryAsync();

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task GetRegistryAsync_WhenResponseBodyIsNull_Throws()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null")
                {
                    Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") }
                }
            });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var client = CreateSut(httpClient, new Mock<ICorrelationHeaderStamper>().Object);

        await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.GetRegistryAsync());
    }

    // ---------------------------------------------------------------------------
    // ExecuteStepAsync by name (untyped)
    // ---------------------------------------------------------------------------

    private static (KaleidoProcessorClient Client, List<(HttpRequestMessage Request, string? Body)> Sent) CreateStepClient(
        Mock<ICorrelationHeaderStamper>? stamper = null,
        HttpStatusCode stepStatus = HttpStatusCode.OK)
    {
        var sent = new List<(HttpRequestMessage, string?)>();
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal))
                {
                    return JsonOk(FakeRegistry);
                }

                sent.Add((req, req.Content?.ReadAsStringAsync(CancellationToken.None).GetAwaiter().GetResult()));

                return stepStatus == HttpStatusCode.OK
                    ? JsonOk(new StepExecutionResponse<object>
                    {
                        ProcessId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        StepName = "MyStep",
                        Result = new { visitId = "V-1" }
                    })
                    : new HttpResponseMessage(stepStatus);
            });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var client = CreateSut(httpClient, (stamper ?? new Mock<ICorrelationHeaderStamper>()).Object);
        return (client, sent);
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_PostsTheJsonPayload_ToTheStepsRegistryUrl()
    {
        var (client, sent) = CreateStepClient();
        var step = System.Text.Json.JsonSerializer.SerializeToElement(new { memberId = "M1" });

        var response = await client.ExecuteStepAsync("mystep", step);

        var (request, body) = Assert.Single(sent);
        Assert.Equal("/processes/steps/mystep", request.RequestUri!.AbsolutePath);
        Assert.Contains("\"processStep\":{\"memberId\":\"M1\"}", body, StringComparison.Ordinal);
        Assert.Equal("MyStep", response.StepName);
        Assert.Equal("V-1", response.Result.GetProperty("visitId").GetString());
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_AnExplicitProcessId_WinsOverTheAmbientOne()
    {
        var explicitId = Guid.NewGuid();
        var stamper = new Mock<ICorrelationHeaderStamper>();
        stamper.Setup(x => x.Stamp(It.IsAny<HttpRequestMessage>()))
            .Callback((HttpRequestMessage r) => r.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, Guid.NewGuid().ToString()));
        var (client, sent) = CreateStepClient(stamper);

        await client.ExecuteStepAsync("MyStep", System.Text.Json.JsonSerializer.SerializeToElement(new { }), explicitId);

        Assert.Equal(explicitId.ToString(), Assert.Single(Assert.Single(sent).Request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_WithoutAProcessId_KeepsTheAmbientOne()
    {
        var ambient = Guid.NewGuid().ToString();
        var stamper = new Mock<ICorrelationHeaderStamper>();
        stamper.Setup(x => x.Stamp(It.IsAny<HttpRequestMessage>()))
            .Callback((HttpRequestMessage r) => r.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, ambient));
        var (client, sent) = CreateStepClient(stamper);

        await client.ExecuteStepAsync("MyStep", System.Text.Json.JsonSerializer.SerializeToElement(new { }));

        Assert.Equal(ambient, Assert.Single(Assert.Single(sent).Request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_AnUnknownStep_Throws_NotFound()
    {
        var (client, _) = CreateStepClient();

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync("NoSuchStep", System.Text.Json.JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(HttpClientErrorCodes.NotFound, exception.Code);
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_AFailedCall_Throws_RequestFailed()
    {
        var (client, _) = CreateStepClient(stepStatus: HttpStatusCode.Conflict);

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync("MyStep", System.Text.Json.JsonSerializer.SerializeToElement(new { })));

        Assert.Equal(HttpClientErrorCodes.RequestFailed, exception.Code);
        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
    }

    // ---------------------------------------------------------------------------
    // GetProcessStateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetProcessStateAsync_WhenFound_ReturnsState()
    {
        var processId = Guid.NewGuid();
        var fakeState = new ProcessStateResponse
        {
            ProcessId = processId,
            AvailableSteps = [],
            Steps = []
        };

        // First call = GetProcessStateAsync sends one GET (no registry needed for state URL)
        // The state endpoint does not require registry lookup — just build the URL from options
        var (client, _) = CreateClient(respond: _ => JsonOk(fakeState));

        var result = await client.GetProcessStateAsync(processId);

        Assert.NotNull(result);
        Assert.Equal(processId, result!.ProcessId);
    }

    [Fact]
    public async Task GetProcessStateAsync_WhenNotFound_ReturnsNull()
    {
        var (client, _) = CreateClient(respond: _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetProcessStateAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProcessStateAsync_WhenHttpFails_ThrowsKaleidoException()
    {
        var (client, _) = CreateClient(respond: _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.GetProcessStateAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Equal(HttpClientErrorCodes.RequestFailed, ex.Code);
    }

    // ---------------------------------------------------------------------------
    // ExecuteStepAsync (untyped)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_ResolvesStepUrlAndPosts()
    {
        var fakeResult = new StepExecutionResponse
        {
            ProcessId = Guid.NewGuid(),
            StepName = "MyStep",
            BusinessMessages = []
        };

        string? postedUrl = null;
        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callCount++;
            if (callCount == 1)
            {
                return JsonOk(FakeRegistry);
            }

            postedUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(fakeResult);
        });

        var result = await client.ExecuteStepAsync(new MyStep());

        Assert.Equal("MyStep", result.StepName);
        Assert.Contains("/processes/steps/mystep", postedUrl);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenStepNotInRegistry_Throws()
    {
        var (client, _) = CreateClient();

        await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync(new UnknownTypeForTest()));
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenHttpFails_ThrowsKaleidoException()
    {
        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callCount++;
            if (callCount == 1)
            {
                return JsonOk(FakeRegistry);
            }

            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        });

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync(new MyStep()));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Equal(HttpClientErrorCodes.RequestFailed, ex.Code);
    }

    // ---------------------------------------------------------------------------
    // ExecuteStepAsync (typed)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_TypedResult_ReturnsTypedResponse()
    {
        var fakeResult = new StepExecutionResponse<MyStepResponse>
        {
            ProcessId = Guid.NewGuid(),
            StepName = "MyStep",
            BusinessMessages = [],
            Result = new MyStepResponse { Value = "done" }
        };

        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callCount++;
            if (callCount == 1)
            {
                return JsonOk(FakeRegistry);
            }

            return JsonOk(fakeResult);
        });

        var result = await client.ExecuteStepAsync<MyStep, MyStepResponse>(new MyStep());

        Assert.Equal("MyStep", result.StepName);
        Assert.NotNull(result.Result);
        Assert.Equal("done", result.Result!.Value);
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

        await client.GetRegistryAsync();

        Assert.Equal("/radiology/registry", registryUrl);
    }

    [Fact]
    public async Task RoutePrefix_IsAppliedToProcessStateUrl()
    {
        var processId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        string? stateUrl = null;
        var (client, _) = CreateClient(routePrefix: "radiology", respond: req =>
        {
            stateUrl = req.RequestUri!.PathAndQuery;
            // Always 404 for simplicity
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await client.GetProcessStateAsync(processId);

        Assert.Contains("/radiology/processes/", stateUrl);
        Assert.Contains(processId.ToString(), stateUrl);
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

        await client.GetRegistryAsync();

        Assert.Equal("/registry", registryUrl);
    }

    // ---------------------------------------------------------------------------
    // Correlation headers
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetProcessStateAsync_StampsCorrelationHeadersOnRequest()
    {
        var fakeState = new ProcessStateResponse
        {
            ProcessId = Guid.NewGuid(),
            AvailableSteps = [],
            Steps = []
        };
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage _, CancellationToken _) => JsonOk(fakeState));

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var stamper = new Mock<ICorrelationHeaderStamper>();

        var client = CreateSut(httpClient, stamper.Object);
        await client.GetProcessStateAsync(Guid.NewGuid());

        stamper.Verify(x => x.Stamp(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Fake types
    // ---------------------------------------------------------------------------

    // The step name is the type name: "MyStep" matches FakeStep.Name
    private sealed class MyStep { }

    private sealed class UnknownTypeForTest { }

    private sealed class MyStepResponse { public string Value { get; init; } = ""; }
}
