using System.Net;
using System.Text;
using Kaleido.Http.Processor;
using Kaleido.Http.FunctionalTests.Processor.Fixtures;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;

namespace Kaleido.Http.FunctionalTests.Processor.Execution;

[Collection(nameof(ProcessorAspNetCoreSuite))]
public sealed class StepExecutionEndpointTests
{
    private const string RootStepUrl = "/kaleido/processes/steps/runtimerootstep";

    private readonly HttpClient _client;

    public StepExecutionEndpointTests(ProcessorAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PostStepExecute_WithRawEnvelope_UsesOptionalProcessIdHeader(bool resume)
    {
        // Resuming needs an existing process in which the (non-repeatable) root step is still
        // available, so it is created by an independent step instead of the root step.
        var expectedProcessId = resume ? await StartProcessWithoutRootAsync() : (Guid?)null;
        using var request = new HttpRequestMessage(HttpMethod.Post, RootStepUrl)
        {
            Content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json")
        };
        if (expectedProcessId is { } processId)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString());
        }

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var contract = await response.Content.ReadAsync<StepExecutionResponse<RuntimeRootStepResponse>>();
        Assert.NotNull(contract);
        if (expectedProcessId.HasValue)
        {
            Assert.Equal(expectedProcessId.Value, contract.ProcessId);
        }
        Assert.NotEqual(Guid.Empty, contract.ProcessId);
        Assert.True(response.Headers.TryGetValues(KaleidoCorrelationHeaders.ProcessId, out var values));
        Assert.Equal(contract.ProcessId.ToString(), Assert.Single(values));
        Assert.Equal(StepExecutionOutcome.Completed, contract.Outcome);
        Assert.Equal(RuntimeStepNames.Root, contract.Result?.Value);
        Assert.Empty(contract.BusinessMessages);
        Assert.Empty(contract.FrameworkMessages);
    }

    [Fact]
    public async Task PostStepExecute_ReturnsTypedStepResult()
    {
        using var response =
            await PostAsync(
                RootStepUrl,
                new ExecuteStepRequest<RuntimeRootStep> { ProcessStep = new RuntimeRootStep() },
                processId: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<StepExecutionResponse<RuntimeRootStepResponse>>();

        Assert.NotNull(contract);
        Assert.Equal(RuntimeStepNames.Root, contract.StepName);
        Assert.NotNull(contract.Result);
        Assert.Equal(RuntimeStepNames.Root, contract.Result.Value);
    }

    [Fact]
    public async Task PostStepExecute_AcrossRequests_PersistsProcessState()
    {
        var processId = await StartProcessAsync();

        using var executeResponse =
            await PostAsync(
                "/kaleido/processes/steps/runtimestepa",
                new ExecuteStepRequest<RuntimeStepA> { ProcessStep = new RuntimeStepA() },
                processId);

        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);

        using var stateResponse =
            await _client.GetAsync($"/kaleido/processes/{processId}");

        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

        var contract =
            await stateResponse.Content.ReadAsync<ProcessStateResponse>();

        Assert.NotNull(contract);
        Assert.Contains(contract.Steps, x => x.StepName == RuntimeStepNames.Root && x.Status == StepExecutionStatus.Completed);
        Assert.Contains(contract.Steps, x => x.StepName == RuntimeStepNames.StepA && x.Status == StepExecutionStatus.Completed);
        Assert.Contains(contract.AvailableSteps, x => x.Name == RuntimeStepNames.StepB);
    }

    [Fact]
    public async Task PostStepExecute_WithAnUnknownProcessId_IsRejected_AndStartsNothing()
    {
        var unknownProcessId = Guid.NewGuid();

        using var response =
            await PostAsync(
                RootStepUrl,
                new ExecuteStepRequest<RuntimeRootStep> { ProcessStep = new RuntimeRootStep() },
                unknownProcessId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadAsync<KaleidoErrorResponse>();

        Assert.NotNull(error);
        Assert.Contains(error.Errors, x => x.Code == ProcessorErrorCodes.ProcessNotFound);

        using var stateResponse =
            await _client.GetAsync($"/kaleido/processes/{unknownProcessId}");

        Assert.Equal(HttpStatusCode.NotFound, stateResponse.StatusCode);
    }

    // Starts a new process the supported way: no process id, the runtime generates one.
    private async Task<Guid> StartProcessAsync()
    {
        using var response =
            await PostAsync(
                RootStepUrl,
                new ExecuteStepRequest<RuntimeRootStep> { ProcessStep = new RuntimeRootStep() },
                processId: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<StepExecutionResponse<RuntimeRootStepResponse>>();

        Assert.NotNull(contract);
        Assert.NotEqual(Guid.Empty, contract.ProcessId);

        return contract.ProcessId;
    }

    private async Task<Guid> StartProcessWithoutRootAsync()
    {
        using var response =
            await PostAsync(
                "/kaleido/processes/steps/runtimefailingstep",
                new ExecuteStepRequest<RuntimeFailingStep> { ProcessStep = new RuntimeFailingStep() },
                processId: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<StepExecutionResponse>();

        Assert.NotNull(contract);
        Assert.NotEqual(Guid.Empty, contract.ProcessId);

        return contract.ProcessId;
    }

    private async Task<HttpResponseMessage> PostAsync<T>(string url, T body, Guid? processId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        return await _client.SendAsync(request);
    }
}
