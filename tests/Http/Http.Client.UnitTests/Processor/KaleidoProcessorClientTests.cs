using System.Text.Json;
using Kaleido.Http.Processor;
using Kaleido.Platform.Processor;

namespace Kaleido.Http.Client.Processor.UnitTests;

public sealed class KaleidoProcessorClientTests : Kaleido.UnitTests.SutFixture
{
    private static readonly RegistryTransportRoute HttpRoute = new() { Transport = "http", GatewayRoute = "/radiology/1" };
    private readonly Mock<IKaleidoRegistryTargetResolver> targets = new();
    private readonly Mock<IKaleidoClientTransport> transport = new();
    private readonly List<KaleidoClientRequest> sent = [];

    private KaleidoProcessorClient CreateSut(string? version = null)
    {
        var processor = new ProcessorRegistryResponse
        {
            Name = "radiology",
            ServiceName = "radiology",
            ServiceVersion = "1",
            ExecuteUrl = "/radiology/1/processes/execute",
            ProcessStateUrlTemplate = "/radiology/1/processes/{processId}",
            Steps =
            [
                new ProcessStepResponse { Name = "MyStep", ExecuteUrl = "/radiology/1/processes/steps/published-step" },
                new ProcessStepResponse { Name = "FakeStepRequest", ExecuteUrl = "/radiology/1/processes/steps/fakesteprequest" },
            ],
        };
        targets.Setup(x => x.ResolveAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KaleidoRegistryTarget(new KaleidoRegistry { Processes = [processor] },
                new RegistryServiceAvailability { ServiceName = "radiology", ServiceVersion = "1", IsCurrent = true, Transports = [HttpRoute] }, HttpRoute));
        transport.Setup(x => x.SendAsync<ProcessExecutionResponse>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResponse { ProcessId = Guid.NewGuid() });
        transport.Setup(x => x.SendAsync<StepExecutionResponse>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepExecutionResponse { ProcessId = Guid.NewGuid(), StepName = "MyStep" });
        transport.Setup(x => x.SendAsync<StepExecutionResponse<MyStepResponse>>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepExecutionResponse<MyStepResponse> { ProcessId = Guid.NewGuid(), StepName = "MyStep", Result = new MyStepResponse { Value = "typed" } });
        transport.Setup(x => x.SendAsync<StepExecutionResponse<JsonElement>>(Captured(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StepExecutionResponse<JsonElement> { ProcessId = Guid.NewGuid(), StepName = "MyStep", Result = JsonSerializer.SerializeToElement(new { visitId = "V-1" }) });
        return new KaleidoProcessorClient("radiology", version, targets.Object, transport.Object);
    }

    private KaleidoClientRequest Captured() => Capture.In(sent);
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);
    private sealed class MyStep;
    private sealed record FakeStepRequest;
    private sealed class MyStepResponse { public string Value { get; init; } = ""; }

    [Fact]
    public async Task GetProcessStateAsync_ExpandsThePublishedTemplate_AsANotFoundCall_WithoutAKey()
    {
        var processId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var state = new ProcessStateResponse { ProcessId = processId, AvailableSteps = [], Steps = [] };
        transport.Setup(x => x.SendOrNotFoundAsync<ProcessStateResponse>(Captured(), Cancel)).ReturnsAsync(state);

        var result = await CreateSut().GetProcessStateAsync(processId, Cancel);

        Assert.Same(state, result);
        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(ProcessContractUrls.ProcessState("radiology", "1", processId), request.Url);
        Assert.Equal("radiology", request.ServiceName);
        Assert.Null(request.Body);
        Assert.False(request.Idempotent);
    }

    [Fact]
    public async Task ExecuteAsync_UsesThePublishedExecuteUrl_WithTheCallersKey()
    {
        var body = new ExecuteProcessRequest();

        await CreateSut().ExecuteAsync(body, "order-42", Cancel);

        var request = Assert.Single(sent);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/radiology/1/processes/execute", request.Url);
        Assert.Same(body, request.Body);
        Assert.Equal("order-42", request.IdempotencyKey);
        Assert.True(request.Idempotent);
    }

    [Fact]
    public async Task ExecuteStepAsync_UsesThePublishedStepUrl_NotTheStepNameAsAPath()
    {
        var step = new MyStep();

        var result = await CreateSut().ExecuteStepAsync(step, Cancel);

        Assert.Equal("MyStep", result.StepName);
        var request = Assert.Single(sent);
        Assert.Equal("/radiology/1/processes/steps/published-step", request.Url);
        Assert.Same(step, Assert.IsType<ExecuteStepRequest<MyStep>>(request.Body).ProcessStep);
        Assert.True(request.Idempotent);
    }

    [Fact]
    public async Task ExecuteStepAsync_TypedResult_ReturnsTheTypedAnswer()
    {
        var result = await CreateSut().ExecuteStepAsync<FakeStepRequest, MyStepResponse>(new FakeStepRequest(), Cancel);

        Assert.Equal("typed", result.Result?.Value);
        Assert.Equal("/radiology/1/processes/steps/fakesteprequest", Assert.Single(sent).Url);
    }

    [Fact]
    public async Task ExecuteStepAsync_ByName_PreservesTheProcessAndTheCallersKey()
    {
        var processId = Guid.NewGuid();

        var response = await CreateSut().ExecuteStepAsync("MyStep", Json(new { memberId = "M1" }), processId, "order-42", Cancel);

        Assert.Equal("V-1", response.Result.GetProperty("visitId").GetString());
        var request = Assert.Single(sent);
        Assert.Equal("/radiology/1/processes/steps/published-step", request.Url);
        Assert.Equal("M1", Assert.IsType<ExecuteStepRequest<JsonElement>>(request.Body).ProcessStep.GetProperty("memberId").GetString());
        Assert.Equal(processId, request.ProcessId);
        Assert.Equal("order-42", request.IdempotencyKey);
        Assert.True(request.Idempotent);
    }

    [Fact]
    public async Task ExecuteStepAsync_AnUnpublishedStep_IsRefusedBeforeSending()
    {
        var exception = await Assert.ThrowsAsync<KaleidoRegistryClientException>(() => CreateSut().ExecuteStepAsync("../registry", Json(new { }), null, null, Cancel));

        Assert.Equal(RegistryClientErrorCodes.RouteUnavailable, exception.Code);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task ExecuteStepAsync_WithoutAName_IsRefused(string? stepName)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateSut().ExecuteStepAsync(stepName!, Json(new { }), null, null, Cancel));
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ExecuteAsync_AFailure_IsPassedOn()
    {
        var failure = new KaleidoHttpClientException(HttpClientErrorCodes.Conflict, "changed", HttpStatusCode.Conflict);
        var sut = CreateSut();
        transport.Setup(x => x.SendAsync<ProcessExecutionResponse>(It.IsAny<KaleidoClientRequest>(), Cancel)).ThrowsAsync(failure);

        Assert.Same(failure, await Assert.ThrowsAsync<KaleidoHttpClientException>(() => sut.ExecuteAsync(new ExecuteProcessRequest(), null, Cancel)));
    }
}
