using System.Text.Json;
using Kaleido.Http.Processor;

namespace Kaleido.Http.Client.Processor.UnitTests;

// What the processor client sends (#498): each call's URL, body and flags, handed to the transport,
// which is tested in KaleidoClientTransportTests.
public sealed class KaleidoProcessorClientTests
    : Kaleido.UnitTests.SutFixture
{
    private readonly Mock<IKaleidoClientTransport> transport = new();
    private readonly Mock<IKaleidoRemoteRegistry> remoteRegistry = new();
    private readonly ICorrelationHeaderStamper stamper = Mock.Of<ICorrelationHeaderStamper>();
    private readonly List<KaleidoClientRequest> sent = [];

    private KaleidoProcessorClient CreateSut(string routePrefix = "radiology")
    {
        var catalog = new Mock<IKaleidoClientCatalog>();
        catalog.Setup(x => x.Find("RadiologyClient")).Returns(Definition(routePrefix));

        transport.Setup(x => x.SendAsync<ProcessExecutionResponse>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResponse { ProcessId = Guid.NewGuid() });
        transport.Setup(x => x.SendAsync<StepExecutionResponse>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepExecutionResponse { ProcessId = Guid.NewGuid(), StepName = "MyStep" });
        transport.Setup(x => x.SendAsync<StepExecutionResponse<MyStepResponse>>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepExecutionResponse<MyStepResponse> { ProcessId = Guid.NewGuid(), StepName = "MyStep", Result = new MyStepResponse { Value = "typed" } });
        transport.Setup(x => x.SendAsync<StepExecutionResponse<JsonElement>>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepExecutionResponse<JsonElement> { ProcessId = Guid.NewGuid(), StepName = "MyStep", Result = JsonSerializer.SerializeToElement(new { visitId = "V-1" }) });

        return new("RadiologyClient", catalog.Object, transport.Object, remoteRegistry.Object, stamper);
    }

    private KaleidoClientRequest Captured() =>
        Capture.In(sent);

    private static KaleidoClientDefinition Definition(string routePrefix) =>
        new("RadiologyClient", routePrefix, RegistryTtl: null, StrictRegistryProbe: false, HealthCheckTimeout: TimeSpan.FromSeconds(5));

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    private sealed class MyStep;

    private sealed record FakeStepRequest;

    private sealed class MyStepResponse
    {
        public string Value { get; init; } = "";
    }

    // ---------------------------------------------------------------------------
    // Registry
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_ReturnsTheProcessesOfTheClientsRemoteRegistry()
    {
        var sut = CreateSut();
        remoteRegistry.Setup(x => x.GetAsync(Definition("radiology"), stamper, Cancel))
            .ReturnsAsync(new AggregatedRegistryResponse { Processes = [new ProcessorRegistryResponse { Name = "radiology", ServiceName = "radiology" }] });

        var result = await sut.GetRegistryAsync(Cancel);

        Assert.Equal("radiology", Assert.Single(result).Name);
    }

    [Fact]
    public async Task InvalidateRegistryAsync_DropsTheClientsRemoteRegistry()
    {
        await CreateSut().InvalidateRegistryAsync(Cancel);

        remoteRegistry.Verify(x => x.InvalidateAsync("RadiologyClient", Cancel), Times.Once);
    }

    // ---------------------------------------------------------------------------
    // Process state
    // ---------------------------------------------------------------------------

    // 404 is an unknown process here: answered null, and the registry is left alone.
    [Fact]
    public async Task GetProcessStateAsync_GetsTheStateUrl_AsANotFoundCall_WithoutAKey()
    {
        var processId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var state = new ProcessStateResponse { ProcessId = processId, AvailableSteps = [], Steps = [] };
        transport.Setup(x => x.SendOrNotFoundAsync<ProcessStateResponse>(Captured(), Cancel)).ReturnsAsync(state);
        var sut = CreateSut();

        var result = await sut.GetProcessStateAsync(processId, Cancel);

        Assert.Same(state, result);
        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(ProcessContractUrls.ProcessState("radiology", processId), request.Url);
        Assert.Null(request.Body);
        Assert.False(request.Idempotent);
        Assert.False(request.InvalidatesRegistryOnNotFound);
    }

    // ---------------------------------------------------------------------------
    // Execute
    // ---------------------------------------------------------------------------

    // Every execute call is idempotent (the caller's key, else its own) and a 404 drops the snapshot.
    [Fact]
    public async Task ExecuteAsync_PostsTheRequestToTheExecuteUrl_WithTheCallersKey()
    {
        var body = new ExecuteProcessRequest();
        var sut = CreateSut();

        await sut.ExecuteAsync(body, "order-42", Cancel);

        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(ProcessContractUrls.Execute("radiology"), request.Url);
        Assert.Same(body, request.Body);
        Assert.Equal("order-42", request.IdempotencyKey);
        Assert.True(request.Idempotent);
        Assert.True(request.InvalidatesRegistryOnNotFound);
        Assert.Equal("Process execute request", request.Description);
    }

    [Fact]
    public async Task ExecuteStepAsync_PostsTheStepToItsUrl_NamedByItsType()
    {
        var step = new MyStep();
        var sut = CreateSut();

        var result = await sut.ExecuteStepAsync(step, Cancel);

        Assert.Equal("MyStep", result.StepName);
        var request = Assert.Single(sent);
        Assert.Equal("/radiology/processes/steps/mystep", request.Url);
        Assert.Same(step, Assert.IsType<ExecuteStepRequest<MyStep>>(request.Body).ProcessStep);
        Assert.True(request.Idempotent);
        Assert.True(request.InvalidatesRegistryOnNotFound);
        Assert.Null(request.IdempotencyKey);
    }

    [Fact]
    public async Task ExecuteStepAsync_TypedResult_ReturnsTheTypedAnswer()
    {
        var sut = CreateSut();

        var result = await sut.ExecuteStepAsync<FakeStepRequest, MyStepResponse>(new FakeStepRequest(), Cancel);

        Assert.Equal("typed", result.Result?.Value);
        Assert.Equal("/radiology/processes/steps/fakesteprequest", Assert.Single(sent).Url);
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_PostsTheJsonPayload_WithTheProcessAndTheCallersKey()
    {
        var processId = Guid.NewGuid();
        var sut = CreateSut();

        var response = await sut.ExecuteStepAsync("MyStep", Json(new { memberId = "M1" }), processId, "order-42", Cancel);

        Assert.Equal("V-1", response.Result.GetProperty("visitId").GetString());
        var request = Assert.Single(sent);
        Assert.Equal("/radiology/processes/steps/mystep", request.Url);
        Assert.Equal("M1", Assert.IsType<ExecuteStepRequest<JsonElement>>(request.Body).ProcessStep.GetProperty("memberId").GetString());
        Assert.Equal(processId, request.ProcessId);
        Assert.Equal("order-42", request.IdempotencyKey);
        Assert.True(request.Idempotent);
        Assert.Equal("Process step request for 'MyStep'", request.Description);
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_KeepsTheNameOneRouteSegment()
    {
        await CreateSut().ExecuteStepAsync("../registry", Json(new { }), null, null, Cancel);

        Assert.Equal("/radiology/processes/steps/..%2Fregistry", Assert.Single(sent).Url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task ExecuteStepAsync_ByName_WithoutAName_IsRefused(string? stepName)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateSut().ExecuteStepAsync(stepName!, Json(new { }), null, null, Cancel));

        Assert.Empty(sent);
    }

    // Step URLs come from the route prefix, never from the registry (#381).
    [Fact]
    public async Task ExecuteStepAsync_WithoutARoutePrefix_UsesTheDefaultUrl_AndReadsNoRegistry()
    {
        await CreateSut(routePrefix: "").ExecuteStepAsync("MyStep", Json(new { }), null, null, Cancel);

        Assert.Equal("/processes/steps/mystep", Assert.Single(sent).Url);
        remoteRegistry.Verify(x => x.GetAsync(It.IsAny<KaleidoClientDefinition>(), It.IsAny<ICorrelationHeaderStamper>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // A failure is the transport's answer, passed on as it is.
    [Fact]
    public async Task ExecuteAsync_AFailure_IsPassedOn()
    {
        var failure = new KaleidoHttpClientException(HttpClientErrorCodes.Conflict, "changed", HttpStatusCode.Conflict);
        var sut = CreateSut();
        transport.Setup(x => x.SendAsync<ProcessExecutionResponse>(It.IsAny<KaleidoClientRequest>(), Cancel)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.ExecuteAsync(new ExecuteProcessRequest(), null, Cancel)));
    }
}
