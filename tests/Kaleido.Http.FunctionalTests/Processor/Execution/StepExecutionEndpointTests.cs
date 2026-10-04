using System.Net;
using System.Text;
using Kaleido.Http.Processor;
using Kaleido.Http.FunctionalTests.Processor.Fixtures;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;

namespace Kaleido.Http.FunctionalTests.Processor.Execution;

[Collection(nameof(ProcessorAspNetCoreSuite))]
public sealed class StepExecutionEndpointTests
{
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
        var expectedProcessId = resume ? Guid.NewGuid() : (Guid?)null;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/kaleido/processes/steps/runtimeroot")
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
        var response =
            await PostWithProcessIdAsync(
                "/kaleido/processes/steps/runtimeroot",
                new ExecuteStepRequest<RuntimeRootStep> { ProcessStep = new RuntimeRootStep() },
                Guid.NewGuid());

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
        var processId = Guid.NewGuid();

        await PostWithProcessIdAsync(
            "/kaleido/processes/steps/runtimeroot",
            new ExecuteStepRequest<RuntimeRootStep> { ProcessStep = new RuntimeRootStep() },
            processId);

        var executeResponse =
            await PostWithProcessIdAsync(
                "/kaleido/processes/steps/runtimestepa",
                new ExecuteStepRequest<RuntimeStepA> { ProcessStep = new RuntimeStepA() },
                processId);

        Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);

        var stateResponse =
            await _client.GetAsync($"/kaleido/processes/{processId}");

        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);

        var contract =
            await stateResponse.Content.ReadAsync<ProcessStateResponse>();

        Assert.NotNull(contract);
        Assert.Contains(contract.Steps, x => x.StepName == RuntimeStepNames.Root && x.Status == StepExecutionStatus.Completed);
        Assert.Contains(contract.Steps, x => x.StepName == RuntimeStepNames.StepA && x.Status == StepExecutionStatus.Completed);
        Assert.Contains(contract.AvailableSteps, x => x.Name == RuntimeStepNames.StepB);
    }

    private Task<HttpResponseMessage> PostWithProcessIdAsync<T>(string url, T body, Guid processId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, processId.ToString());
        return _client.SendAsync(request);
    }
}
