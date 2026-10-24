using System.Text.Json;
using Kaleido.Http.Processor;
using Microsoft.Extensions.Logging;
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
        string serviceName = "",
        ILogger<KaleidoProcessorClient>? logger = null)
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
            logger ?? NullLogger<KaleidoProcessorClient>.Instance,
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
        store.Setup(s => s.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string k, CancellationToken _) =>
            {
                data.TryRemove(k, out var _);
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

        var result = await client.GetRegistryAsync(TestContext.Current.CancellationToken);

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
            () => client.GetRegistryAsync(TestContext.Current.CancellationToken));
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

        var response = await client.ExecuteStepAsync("mystep", step, null, null, TestContext.Current.CancellationToken);

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

        await client.ExecuteStepAsync("MyStep", System.Text.Json.JsonSerializer.SerializeToElement(new { }), explicitId, null, TestContext.Current.CancellationToken);

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

        await client.ExecuteStepAsync("MyStep", System.Text.Json.JsonSerializer.SerializeToElement(new { }), null, null, TestContext.Current.CancellationToken);

        Assert.Equal(ambient, Assert.Single(Assert.Single(sent).Request.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
    }

    // An unknown (or forbidden) step is the service's own answer, not a client-side registry check.
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task ExecuteStepAsync_ByName_AStepTheServiceRefuses_GetsItsOwnStatus(HttpStatusCode status)
    {
        var (client, sent) = CreateStepClient(stepStatus: status);

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync("NoSuchStep", System.Text.Json.JsonSerializer.SerializeToElement(new { }), null, null, TestContext.Current.CancellationToken));

        Assert.Equal(HttpClientErrorCodes.RequestFailed, exception.Code);
        Assert.Equal(status, exception.StatusCode);
        Assert.Equal("/processes/steps/nosuchstep", Assert.Single(sent).Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_KeepsTheNameOneRouteSegment()
    {
        var (client, sent) = CreateStepClient();

        await client.ExecuteStepAsync("../registry", System.Text.Json.JsonSerializer.SerializeToElement(new { }), null, null, TestContext.Current.CancellationToken);

        Assert.Equal("/processes/steps/..%2Fregistry", Assert.Single(sent).Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_AFailedCall_Throws_RequestFailed()
    {
        var (client, _) = CreateStepClient(stepStatus: HttpStatusCode.Conflict);

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync("MyStep", System.Text.Json.JsonSerializer.SerializeToElement(new { }), null, null, TestContext.Current.CancellationToken));

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

        var result = await client.GetProcessStateAsync(processId, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(processId, result!.ProcessId);
    }

    [Fact]
    public async Task GetProcessStateAsync_WhenNotFound_ReturnsNull()
    {
        var (client, _) = CreateClient(respond: _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProcessStateAsync_WhenHttpFails_ThrowsKaleidoException()
    {
        var (client, _) = CreateClient(respond: _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

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
        var (client, _) = CreateClient(respond: req =>
        {
            postedUrl = req.RequestUri!.PathAndQuery;
            return JsonOk(fakeResult);
        });

        var result = await client.ExecuteStepAsync(new MyStep(), TestContext.Current.CancellationToken);

        Assert.Equal("MyStep", result.StepName);
        Assert.Equal("/processes/steps/mystep", postedUrl);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenTheServiceDoesNotHaveTheStep_Throws()
    {
        var (client, _) = CreateClient(respond: _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync(new UnknownTypeForTest(), TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
        Assert.Equal(HttpClientErrorCodes.RequestFailed, ex.Code);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenHttpFails_ThrowsKaleidoException()
    {
        var (client, _) = CreateClient(respond: _ => new HttpResponseMessage(HttpStatusCode.BadGateway));

        var ex = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync(new MyStep(), TestContext.Current.CancellationToken));

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

        var (client, _) = CreateClient(respond: _ => JsonOk(fakeResult));

        var result = await client.ExecuteStepAsync<MyStep, MyStepResponse>(new MyStep(), TestContext.Current.CancellationToken);

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

        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

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

        await client.GetProcessStateAsync(processId, TestContext.Current.CancellationToken);

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

        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

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
        await client.GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        stamper.Verify(x => x.Stamp(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Fake types
    // ---------------------------------------------------------------------------

    // The step name is the type name: "MyStep" matches FakeStep.Name
    private sealed class MyStep { }

    private sealed class UnknownTypeForTest { }

    private sealed class MyStepResponse { public string Value { get; init; } = ""; }

    // A 404 from a step means the service was deployed without it: the registry is loaded again.
    [Fact]
    public async Task ExecuteAsync_WhenTheServiceAnswers404_DropsTheRegistrySnapshot()
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
        await Assert.ThrowsAsync<KaleidoHttpClientException>(() => client.ExecuteAsync(new ExecuteProcessRequest(), null, TestContext.Current.CancellationToken));
        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, registryReads);
    }

    // The state endpoint answers 404 for an unknown process, which says nothing about the registry.
    [Fact]
    public async Task GetProcessStateAsync_WhenNotFound_KeepsTheRegistrySnapshot()
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
        await client.GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        await client.GetRegistryAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, registryReads);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheProcessWasChangedConcurrently_ThrowsAConflictWithTheServiceErrors()
    {
        var (client, _) = CreateClient(respond: req =>
            req.RequestUri!.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal)
                ? JsonOk(FakeRegistry)
                : new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(
                        new KaleidoErrorResponse([new KaleidoError("pro_process_conflict", "Changed by another request.")]),
                        options: KaleidoJsonOptions.Options),
                });

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => client.ExecuteAsync(new ExecuteProcessRequest(), null, TestContext.Current.CancellationToken));

        Assert.Equal(HttpClientErrorCodes.Conflict, exception.Code);
        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.Equal("pro_process_conflict", Assert.Single(exception.Errors).Code);
    }

    // One key per execute call: a retry of the same request carries the same key, a new call a new one.
    [Fact]
    public async Task ExecuteAsync_SendsANewIdempotencyKeyWithEveryCall()
    {
        var keys = new List<string>();
        var (client, _) = CreateClient(respond: req =>
        {
            keys.Add(req.Headers.GetValues(KaleidoIdempotencyHeaders.IdempotencyKey).Single());
            return JsonOk(new ProcessExecutionResponse { ProcessId = Guid.NewGuid() });
        });

        await client.ExecuteAsync(new ExecuteProcessRequest(), null, TestContext.Current.CancellationToken);
        await client.ExecuteAsync(new ExecuteProcessRequest(), null, TestContext.Current.CancellationToken);

        Assert.Equal(2, keys.Count);
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));
        Assert.NotEqual(keys[0], keys[1]);
    }

    // #440: a caller's own key for a logical call is sent as is, on both the multi-step and the by-name
    // call, so repeating the call repeats the key; a blank key is no key (one is generated).
    [Theory]
    [InlineData("order-42-confirm", "order-42-confirm")]
    [InlineData("  ", null)]
    public async Task ExecuteAsync_AndExecuteStepAsyncByName_SendTheCallersIdempotencyKey(string given, string? expected)
    {
        var keys = new List<string>();
        var (client, _) = CreateClient(respond: req =>
        {
            keys.Add(req.Headers.GetValues(KaleidoIdempotencyHeaders.IdempotencyKey).Single());
            return req.RequestUri!.AbsolutePath.EndsWith("/execute", StringComparison.Ordinal)
                ? JsonOk(new ProcessExecutionResponse { ProcessId = Guid.NewGuid() })
                : JsonOk(new StepExecutionResponse<JsonElement> { ProcessId = Guid.NewGuid(), StepName = "MyStep", Result = JsonSerializer.SerializeToElement(new { }) });
        });

        await client.ExecuteAsync(new ExecuteProcessRequest(), given, TestContext.Current.CancellationToken);
        await client.ExecuteStepAsync("MyStep", JsonSerializer.SerializeToElement(new { }), null, given, TestContext.Current.CancellationToken);

        Assert.Equal(2, keys.Count);
        Assert.All(keys, key => Assert.False(string.IsNullOrWhiteSpace(key)));

        if (expected is not null)
        {
            Assert.All(keys, key => Assert.Equal(expected, key));
        }
        else
        {
            Assert.NotEqual(keys[0], keys[1]);
        }
    }

    [Fact]
    public async Task ExecuteStepAsync_TypedResult_WhenTheServiceRejectsTheStep_ThrowsValidationFailedWithTheErrors()
    {
        var (client, _) = CreateClient(respond: req =>
            req.RequestUri!.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal)
                ? JsonOk(FakeRegistry)
                : new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = JsonContent.Create(
                        new KaleidoErrorResponse([new KaleidoError("pro_invalid_step", "Field required.")]),
                        options: KaleidoJsonOptions.Options),
                });

        var exception = await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.ExecuteStepAsync<MyStep, MyStepResponse>(new MyStep(), TestContext.Current.CancellationToken));

        Assert.Equal(HttpClientErrorCodes.ValidationFailed, exception.Code);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("pro_invalid_step", Assert.Single(exception.Errors).Code);
        Assert.Equal("Process step request failed: Field required.", exception.Message);
    }

    // Step calls never read the registry (#381): with an empty cache they make no registry request.
    [Fact]
    public async Task ExecuteStepAsync_MakesNoRegistryRequest_AndUsesTheRoutePrefix()
    {
        var paths = new List<string>();
        var (client, _) = CreateClient(routePrefix: "radiology", respond: req =>
        {
            paths.Add(req.RequestUri!.AbsolutePath);
            return JsonOk(new StepExecutionResponse<object> { ProcessId = Guid.NewGuid(), StepName = "MyStep" });
        });

        await client.ExecuteStepAsync("MyStep", JsonSerializer.SerializeToElement(new { }), processId: null, idempotencyKey: null, cancellationToken: TestContext.Current.CancellationToken);
        await client.ExecuteStepAsync<FakeStepRequest>(new FakeStepRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(["/radiology/processes/steps/mystep", "/radiology/processes/steps/fakesteprequest"], paths);
    }

    // The registry is filtered per caller: a cached view that lacks the step (another caller fetched
    // it) does not stop a caller who may run the step (#381).
    [Fact]
    public async Task ExecuteStepAsync_WhenTheCachedRegistryLacksTheStep_StillCallsIt()
    {
        string? postedPath = null;
        var (client, _) = CreateClient(respond: req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/registry", StringComparison.Ordinal))
            {
                return JsonOk(new AggregatedRegistryResponse { Processes = [] });
            }

            postedPath = req.RequestUri.AbsolutePath;
            return JsonOk(new StepExecutionResponse<object> { ProcessId = Guid.NewGuid(), StepName = "MyStep" });
        });

        Assert.Empty(await client.GetRegistryAsync(TestContext.Current.CancellationToken));
        await client.ExecuteStepAsync("MyStep", JsonSerializer.SerializeToElement(new { }), processId: null, idempotencyKey: null, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("/processes/steps/mystep", postedPath);
    }

    private sealed record FakeStepRequest;

    private static (KaleidoProcessorClient Client, RecordingLogger Logger) CreateLoggedClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken _) => respond(req));
        var httpClient = new HttpClient(handler.Object) { BaseAddress = new Uri("http://localhost") };
        var logger = new RecordingLogger();

        return (CreateSut(httpClient, Mock.Of<ICorrelationHeaderStamper>(), "radiology", logger), logger);
    }

    [Fact]
    public async Task ExecuteAsync_LogsTheOutgoingCallAtDebug()
    {
        var (client, logger) = CreateLoggedClient(_ => JsonOk(new ProcessExecutionResponse { ProcessId = Guid.NewGuid() }));

        await client.ExecuteAsync(new ExecuteProcessRequest(), null, TestContext.Current.CancellationToken);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Contains("Sending POST", entry.Message, StringComparison.Ordinal);
        Assert.Contains("remote processor 'radiology'", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetProcessStateAsync_WhenTheServiceFails_LogsAWarningWithTheStatus()
    {
        var (client, logger) = CreateLoggedClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsAsync<KaleidoHttpClientException>(
            () => client.GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Collection(
            logger.Entries,
            sending => Assert.Equal(LogLevel.Debug, sending.Level),
            failed =>
            {
                Assert.Equal(LogLevel.Warning, failed.Level);
                Assert.Contains("Remote processor 'radiology' returned 503 (ServiceUnavailable) for GET", failed.Message, StringComparison.Ordinal);
                Assert.Contains("/radiology/processes/", failed.Message, StringComparison.Ordinal);
            });
    }

    private sealed class RecordingLogger : ILogger<KaleidoProcessorClient>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task GetProcessStateAsync_SendsNoIdempotencyKey()
    {
        var hasKey = true;
        var (client, _) = CreateClient(respond: req =>
        {
            hasKey = req.Headers.Contains(KaleidoIdempotencyHeaders.IdempotencyKey);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        await client.GetProcessStateAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(hasKey);
    }
}
