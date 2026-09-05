using Kaleido.Observability;
using Kaleido.Process.Registry;
using Moq.Protected;

namespace Kaleido.Process.AspNetCore.Client.Tests;

public sealed class KaleidoProcessClientTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static readonly ProcessStepResponse FakeStep = new()
    {
        Name = "MyStep",
        Description = "Test step.",
        Version = "1.0",
        ExecuteUrl = "/processes/steps/mystep",
        MetadataUrl = "/processes/steps/mystep/metadata",
        Fields = [],
        Dependencies = [],
        AvailableAfter = [],
        AvailableUntil = []
    };

    private static readonly ProcessorRegistryResponse FakeProcessor = new()
    {
        Name = "test-processor",
        Description = "Test processor.",
        DisplayName = "Test Processor",
        Version = "1.0",
        RegistryUrl = "/processes/registry",
        Steps = [FakeStep],
        InitialSteps = []
    };

    private static HttpResponseMessage JsonOk<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value)
        };

    private static Mock<HttpMessageHandler> HandlerWithSequence(
        IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> responses)
    {
        var queue = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
                queue.Count > 0 ? queue.Dequeue()(req) : JsonOk(new[] { FakeProcessor }));
        return mock;
    }

    private static (KaleidoProcessClient client, Mock<HttpMessageHandler> handler) CreateClient(
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
                respond != null ? respond(req) : JsonOk(new[] { FakeProcessor }));

        var httpClient = new HttpClient(mock.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());

        var client = new KaleidoProcessClient(httpClient, correlation.Object, routePrefix);
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
            return JsonOk(new[] { FakeProcessor });
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
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext());
        var client = new KaleidoProcessClient(httpClient, correlation.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetRegistryAsync());
    }

    // ---------------------------------------------------------------------------
    // GetStepMetadataAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetStepMetadataAsync_ResolvesStepAndFetchesMetadataUrl()
    {
        var callUrls = new List<string>();
        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callUrls.Add(req.RequestUri!.PathAndQuery);
            callCount++;
            if (callCount == 1)
                return JsonOk(new[] { FakeProcessor });
            return JsonOk(FakeStep);
        });

        var result = await client.GetStepMetadataAsync("MyStep");

        Assert.Equal("MyStep", result.Name);
        Assert.Contains(callUrls, u => u.Contains("metadata"));
    }

    [Fact]
    public async Task GetStepMetadataAsync_WhenStepNotFound_Throws()
    {
        var (client, _) = CreateClient();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetStepMetadataAsync("NoSuchStep"));

        Assert.Contains("NoSuchStep", ex.Message);
    }

    [Fact]
    public async Task GetStepMetadataAsync_WhenHttpFails_ThrowsKaleidoException()
    {
        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callCount++;
            if (callCount == 1)
                return JsonOk(new[] { FakeProcessor });
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var ex = await Assert.ThrowsAsync<KaleidoProcessClientException>(
            () => client.GetStepMetadataAsync("MyStep"));

        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
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

        var ex = await Assert.ThrowsAsync<KaleidoProcessClientException>(
            () => client.GetProcessStateAsync(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
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
            Messages = []
        };

        string? postedUrl = null;
        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callCount++;
            if (callCount == 1)
                return JsonOk(new[] { FakeProcessor });
            postedUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(fakeResult);
        });

        // MyStep type name — "Step" suffix stripped: MyStep → MyStep (no suffix here, keep as-is)
        // Actually step name lookup uses type name with optional "Step" suffix stripping.
        // Our type below is named MyClientStep → strips to MyClient, won't match "MyStep".
        // Use a type whose name without "Step" suffix matches our FakeStep name "MyStep".
        var result = await client.ExecuteStepAsync(new MyStepStep());

        Assert.Equal("MyStep", result.StepName);
        Assert.Contains("/processes/steps/mystep", postedUrl);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenStepNotInRegistry_Throws()
    {
        var (client, _) = CreateClient();

        await Assert.ThrowsAsync<InvalidOperationException>(
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
                return JsonOk(new[] { FakeProcessor });
            return new HttpResponseMessage(HttpStatusCode.BadGateway);
        });

        var ex = await Assert.ThrowsAsync<KaleidoProcessClientException>(
            () => client.ExecuteStepAsync(new MyStepStep()));

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
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
            Messages = [],
            Result = new MyStepResponse { Value = "done" }
        };

        var callCount = 0;
        var (client, _) = CreateClient(respond: req =>
        {
            callCount++;
            if (callCount == 1)
                return JsonOk(new[] { FakeProcessor });
            return JsonOk(fakeResult);
        });

        var result = await client.ExecuteStepAsync<MyStepStep, MyStepResponse>(new MyStepStep());

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
            return JsonOk(new[] { FakeProcessor });
        });

        await client.GetRegistryAsync();

        Assert.Equal("/radiology/processes/registry", registryUrl);
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
            return JsonOk(new[] { FakeProcessor });
        });

        await client.GetRegistryAsync();

        Assert.Equal("/processes/registry", registryUrl);
    }

    // ---------------------------------------------------------------------------
    // Correlation headers
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task StampCorrelationHeaders_WhenContextHasValues_AddsHeaders()
    {
        // GetProcessStateAsync calls StampCorrelationHeaders (unlike GetRegistryAsync which
        // uses GetFromJsonAsync internally). Capture the request sent for the state URL.
        var fakeState = new ProcessStateResponse
        {
            ProcessId = Guid.NewGuid(),
            AvailableSteps = [],
            Steps = []
        };
        HttpRequestMessage? capturedRequest = null;
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) =>
            {
                capturedRequest = req;
                return JsonOk(fakeState);
            });

        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext
        {
            RequestId = "req-456",
            ProcessId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")
        });

        var client = new KaleidoProcessClient(httpClient, correlation.Object);
        await client.GetProcessStateAsync(Guid.NewGuid());

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest!.Headers.TryGetValues(KaleidoCorrelationHeaders.RequestId, out var requestIds));
        Assert.Contains("req-456", requestIds);
        Assert.True(capturedRequest.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out var processIds));
        Assert.Contains("cccccccc-cccc-cccc-cccc-cccccccccccc", processIds);
    }

    // ---------------------------------------------------------------------------
    // Fake types
    // ---------------------------------------------------------------------------

    // "MyStepStep" → strip "Step" suffix → name is "MyStep", matches FakeStep.Name
    private sealed class MyStepStep { }

    private sealed class UnknownTypeForTest { }

    private sealed class MyStepResponse { public string Value { get; init; } = ""; }
}
