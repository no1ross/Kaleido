using System.Security.Claims;
using System.Text.Json;

namespace Kaleido.Process.Http.Client.UnitTests;

public sealed class KaleidoProcessorClientTests : Kaleido.UnitTests.SutFixture, IDisposable
{
    private readonly Mock<IKaleidoRegistryTargetResolver> targets = new();
    private readonly Mock<IHttpClientFactory> clients = new();
    private readonly Mock<IKaleidoInboundCallerAccessor> inboundCaller = new();
    private readonly Mock<IKaleidoCorrelationContextAccessor> correlation = new();
    private readonly Mock<HttpMessageHandler> handler = new(MockBehavior.Strict);
    private readonly List<Sent> sent = [];
    private readonly List<HttpClient> created = [];
    private HttpStatusCode responseStatus = HttpStatusCode.OK;
    private bool emptyResponse;
    private string responseVersion = "1";

    private sealed record Sent(HttpMethod Method, string Path, string? Body, string? Key, string? ProcessId,
        string? RequestId, string? CallingProcessor, string? CallingStep, KaleidoCallContext Call);
    private sealed record MyStep(string Code);
    private sealed record MirrorStep(string Code);
    private static readonly Guid ProcessId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public KaleidoProcessorClientTests()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { RequestId = "process-request" });
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns((HttpRequestMessage request, CancellationToken token) => RespondAsync(request, token));
        clients.Setup(x => x.CreateClient(KaleidoProcessorClient.ClientName)).Returns(() =>
        {
            var client = new HttpClient(handler.Object, disposeHandler: false) { BaseAddress = new Uri("https://gateway.test/") };
            created.Add(client);
            return client;
        });
        Target(Processor());
    }

    private static RegistryTransportBindings Operations(string operation, string method, string path) => new()
    {
        Http = [new RegistryHttpOperation { Operation = operation, Method = method, Path = path }],
    };

    private static ProcessRegistryItem Processor(string version = "1", RegistryTransportBindings? bindings = null,
        RegistryTransportBindings? stepBindings = null) => new()
        {
            Name = "radiology",
            ServiceName = "radiology",
            ServiceVersion = version,
            Bindings = bindings ?? new RegistryTransportBindings
            {
                Http =
            [
                new RegistryHttpOperation { Operation = "execute", Method = "POST", Path = $"/radiology/{version}/processes/execute" },
                new RegistryHttpOperation { Operation = "state", Method = "GET", Path = $"/radiology/{version}/processes/{{processId}}" },
            ],
            },
            Steps = [new ProcessStepResponse { Name = "MyStep", Bindings = stepBindings ?? Operations("execute", "POST", $"/radiology/{version}/processes/steps/published-step") },
            new ProcessStepResponse { Name = "MirrorStep", Bindings = Operations("execute", "POST", $"/radiology/{version}/processes/steps/mirror") }],
        };

    private void Target(ProcessRegistryItem published)
    {
        var route = new RegistryTransportRoute { Transport = "http", GatewayRoute = $"/radiology/{published.ServiceVersion}" };
        targets.Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KaleidoRegistryTarget(new KaleidoRegistry { Processes = [published] },
                new RegistryServiceAvailability { ServiceName = "radiology", ServiceVersion = published.ServiceVersion ?? "1", IsCurrent = true, Transports = [route] }, route));
    }

    private KaleidoProcessorClient CreateSut(string? version = null) =>
        new("radiology", version, targets.Object, clients.Object, inboundCaller.Object, correlation.Object,
            new KaleidoServiceOptions { ServiceName = "origin", ServiceVersion = "1" });

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private async Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request, CancellationToken token)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);
        string? Header(string name) => request.Headers.TryGetValues(name, out var values) ? Assert.Single(values) : null;
        sent.Add(new Sent(request.Method, request.RequestUri?.AbsolutePath ?? string.Empty, body,
            Header(KaleidoIdempotencyHeaders.IdempotencyKey), Header(KaleidoCorrelationHeaders.ProcessId),
            Header(KaleidoCorrelationHeaders.RequestId), Header(KaleidoCorrelationHeaders.CallingProcessor),
            Header(KaleidoCorrelationHeaders.CallingStep), KaleidoCallContext.Of(request)));
        if (responseStatus == HttpStatusCode.NotFound)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (responseStatus != HttpStatusCode.OK)
        {
            return new HttpResponseMessage(responseStatus)
            {
                Content = JsonContent.Create(new KaleidoErrorResponse([new KaleidoError("pro_process_conflict", "Changed")]), options: KaleidoJsonOptions.Options),
            };
        }

        if (emptyResponse)
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json"),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = request.Method == HttpMethod.Get
                ? JsonContent.Create(new ProcessStateSnapshot { ProcessId = ProcessId, ServiceName = "radiology", ServiceVersion = responseVersion }, options: KaleidoJsonOptions.Options)
                : JsonContent.Create(new ProcessResult { ProcessId = ProcessId, ServiceVersion = responseVersion, State = ProcessExecutionState.Active }, options: KaleidoJsonOptions.Options),
        };
    }

    [Fact]
    public async Task GetProcessStateAsync_UsesPublishedStateTemplate_NotAnInferredRoute()
    {
        var snapshot = await CreateSut().GetProcessStateAsync(ProcessId, Cancel);

        Assert.Equal(ProcessId, Assert.IsType<ProcessStateSnapshot>(snapshot).ProcessId);
        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"/radiology/1/processes/{ProcessId:D}", request.Path);
        Assert.Null(request.Body);
        Assert.Null(request.Key);
    }

    [Fact]
    public async Task GetProcessStateAsync_InsideAStep_StampsLocalPerHopIdentityAndAmbientCorrelation()
    {
        var ambient = Guid.NewGuid();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext
        {
            RequestId = "state-request",
            ProcessId = ambient,
            CallingProcessorName = "upstream",
            CallingStepName = "old-step",
            ExecutingStepName = "current-step",
        });

        await CreateSut().GetProcessStateAsync(ProcessId, Cancel);

        var call = Assert.Single(sent);
        Assert.Equal("state-request", call.RequestId);
        Assert.Equal(ambient.ToString("D"), call.ProcessId);
        Assert.Equal("origin", call.CallingProcessor);
        Assert.Equal("current-step", call.CallingStep);
    }

    [Fact]
    public async Task GetProcessStateAsync_OutsideAStep_DoesNotForwardInboundCallingStepHeaders()
    {
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext
        {
            RequestId = "state-request",
            CallingProcessorName = "upstream",
            CallingStepName = "old-step",
        });

        await CreateSut().GetProcessStateAsync(ProcessId, Cancel);

        var call = Assert.Single(sent);
        Assert.Null(call.CallingProcessor);
        Assert.Null(call.CallingStep);
    }

    [Fact]
    public async Task GetProcessStateAsync_WhenRemoteStateIsMissing_ReturnsNullWithoutEvictingTheRegistry()
    {
        responseStatus = HttpStatusCode.NotFound;

        Assert.Null(await CreateSut().GetProcessStateAsync(ProcessId, Cancel));
        targets.Verify(x => x.ResolveAsync("radiology", null, It.IsAny<IReadOnlyCollection<string>>(), Cancel), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ConvertsNeutralStepsAndProcessIdIntoPublishedExecuteCall()
    {
        var request = new ProcessRequest
        {
            ProcessId = ProcessId,
            Processor = new ProcessorRequest
            {
                Steps = new Dictionary<string, object?> { ["MyStep"] = new MyStep("A-1") },
            }
        };

        var result = await CreateSut().ExecuteAsync(request, "operation-42", Cancel);

        Assert.Equal("1", result.ServiceVersion);
        var call = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, call.Method);
        Assert.Equal("/radiology/1/processes/execute", call.Path);
        Assert.Equal(ProcessId.ToString("D"), call.ProcessId);
        Assert.Equal("operation-42", call.Key);
        using var json = JsonDocument.Parse(Assert.IsType<string>(call.Body));
        Assert.Equal("MyStep", json.RootElement.GetProperty("steps")[0].GetProperty("stepName").GetString());
        Assert.Equal("A-1", json.RootElement.GetProperty("steps")[0].GetProperty("request").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteStepAsync_UsesPublishedStepBindingAndGeneratesAStableKeyForThisCall()
    {
        await CreateSut().ExecuteStepAsync(new MyStep("A-2"), null, null, Cancel);

        var call = Assert.Single(sent);
        Assert.Equal("/radiology/1/processes/steps/published-step", call.Path);
        Assert.Null(call.ProcessId);
        Assert.Equal(32, Assert.IsType<string>(call.Key).Length);
        using var json = JsonDocument.Parse(Assert.IsType<string>(call.Body));
        Assert.Equal("A-2", json.RootElement.GetProperty("processStep").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteStepAsync_WithoutACallerKey_CreatesAUniqueKeyForEachLogicalCall()
    {
        var sut = CreateSut();
        await sut.ExecuteStepAsync(new MyStep("A-1"), null, null, Cancel);
        await sut.ExecuteStepAsync(new MyStep("A-2"), null, null, Cancel);

        Assert.InRange(sent.Count, 2, 2);
        Assert.False(string.IsNullOrWhiteSpace(sent[0].Key));
        Assert.False(string.IsNullOrWhiteSpace(sent[1].Key));
        Assert.NotEqual(0, string.CompareOrdinal(sent[0].Key, sent[1].Key));
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_UsesExplicitProcessIdAndLocalCallingStepNotInboundStep()
    {
        var ambient = Guid.NewGuid();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext
        {
            RequestId = "request-12",
            ProcessId = ambient,
            CallingProcessorName = "untrusted-inbound",
            CallingStepName = "untrusted-step",
            ExecutingStepName = "origin-step",
        });

        await CreateSut("1").ExecuteStepAsync("mystep", new MyStep("A-3"), ProcessId, "same-key", Cancel);

        var call = Assert.Single(sent);
        Assert.Equal("/radiology/1/processes/steps/published-step", call.Path);
        Assert.Equal(ProcessId.ToString("D"), call.ProcessId);
        Assert.Equal("same-key", call.Key);
        Assert.Equal("request-12", call.RequestId);
        Assert.Equal("origin", call.CallingProcessor);
        Assert.Equal("origin-step", call.CallingStep);
        targets.Verify(x => x.ResolveAsync("radiology", "1", It.IsAny<IReadOnlyCollection<string>>(), Cancel), Times.Once);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("line\nbreak")]
    public async Task ExecuteStepAsync_AnUnsafeKey_FailsBeforeNetwork(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateSut().ExecuteStepAsync(new MyStep("A-1"), null, key, Cancel));

        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ASuccessfulCallWithoutAPayload_ThrowsEmptyResponse(bool stateRead)
    {
        emptyResponse = true;
        var client = CreateSut();

        var exception = stateRead
            ? await Assert.ThrowsAsync<KaleidoHttpClientException>(() => client.GetProcessStateAsync(ProcessId, Cancel))
            : await Assert.ThrowsAsync<KaleidoHttpClientException>(() => client.ExecuteStepAsync(new MyStep("A-1"), null, null, Cancel));

        Assert.Equal(HttpClientErrorCodes.EmptyResponse, exception.Code);
        Assert.Contains("succeeded but returned no payload", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AnInboundCaller_IsPassedToTheSharedCredentialPipeline()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "alice")], "test"));
        inboundCaller.Setup(x => x.IsInboundRequest).Returns(true);
        inboundCaller.Setup(x => x.Principal).Returns(principal);
        inboundCaller.Setup(x => x.Credential).Returns("Bearer inbound-token");

        await CreateSut().ExecuteStepAsync(new MyStep("A-4"), null, null, Cancel);

        var call = Assert.Single(sent).Call;
        Assert.Equal(KaleidoCallPurpose.OnBehalfOfCaller, call.Purpose);
        Assert.Same(principal, call.Caller);
        Assert.Equal("Bearer inbound-token", call.CallerAuthorization);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenOnlyAnInitialStepIsPublished_UsesItsBinding()
    {
        Target(Processor() with
        {
            Steps = null,
            InitialSteps = [new ProcessStepSummary { Name = "MyStep", Bindings = Operations("execute", "POST", "/radiology/1/processes/steps/initial") }],
        });

        await CreateSut().ExecuteStepAsync(new MyStep("A-1"), null, null, Cancel);

        Assert.Equal("/radiology/1/processes/steps/initial", Assert.Single(sent).Path);
    }

    [Fact]
    public async Task ExecuteAsync_WhenBindingsAreAmbiguous_FailsBeforeAnyCredentialBearingRequest()
    {
        Target(Processor(bindings: new RegistryTransportBindings
        {
            Http =
            [
                new RegistryHttpOperation { Operation = "execute", Method = "POST", Path = "/radiology/1/processes/execute" },
                new RegistryHttpOperation { Operation = "execute", Method = "POST", Path = "/radiology/1/processes/execute-other" },
            ],
        }));

        var failure = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().ExecuteAsync(
            new ProcessRequest { Processor = new ProcessorRequest() }, null, Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, failure.Code);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenNoStepBindingExists_FailsBeforeAnyCredentialBearingRequest()
    {
        Target(Processor(stepBindings: new RegistryTransportBindings()));

        var failure = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().ExecuteStepAsync(new MyStep("A-1"), null, null, Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, failure.Code);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRegistryPublishesAnUnsafePath_FailsBeforeSending()
    {
        Target(Processor(bindings: Operations("execute", "POST", "/radiology/1/../registry")));

        var failure = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().ExecuteAsync(
            new ProcessRequest { Processor = new ProcessorRequest() }, null, Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, failure.Code);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenAServiceVersionIsPinned_UsesThatVersionsPublishedBinding()
    {
        Target(Processor(version: "2"));
        responseVersion = "2";

        var result = await CreateSut("2").ExecuteStepAsync(new MyStep("A-1"), ProcessId, null, Cancel);

        Assert.Equal("2", result.ServiceVersion);
        Assert.Equal("/radiology/2/processes/steps/published-step", Assert.Single(sent).Path);
        targets.Verify(x => x.ResolveAsync("radiology", "2", It.IsAny<IReadOnlyCollection<string>>(), Cancel), Times.Once);
    }

    [Fact]
    public async Task ExecuteStepAsync_WhenGatewayReturnsAnotherVersion_RejectsTheResponse()
    {
        Target(Processor(version: "2"));
        responseVersion = "3";

        var failure = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() =>
            CreateSut("2").ExecuteStepAsync(new MyStep("A-1"), ProcessId, null, Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, failure.Code);
    }

    [Fact]
    public async Task ExecuteAsync_AConflictPreservesTheRemotesError()
    {
        responseStatus = HttpStatusCode.Conflict;

        var failure = await Assert.ThrowsAsync<KaleidoHttpClientException>(() => CreateSut().ExecuteAsync(
            new ProcessRequest { Processor = new ProcessorRequest() }, null, Cancel));

        Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
        Assert.Equal("pro_process_conflict", Assert.Single(failure.Errors).Code);
    }

    public void Dispose()
    {
        foreach (var client in created)
        {
            client.Dispose();
        }
    }
}
