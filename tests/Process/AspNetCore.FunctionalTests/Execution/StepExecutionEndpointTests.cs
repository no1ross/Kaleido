using Kaleido.Observability;
using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Execution;

[Collection(nameof(ProcessAspNetCoreCollection))]
public sealed class StepExecutionEndpointTests
{
    private readonly HttpClient _client;

    public StepExecutionEndpointTests(ProcessAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
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
