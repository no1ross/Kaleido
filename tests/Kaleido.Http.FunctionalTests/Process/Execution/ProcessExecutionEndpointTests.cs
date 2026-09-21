using Kaleido.Http.Process.Contracts;
using Kaleido.Observability;
using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Execution;

[Collection(nameof(ProcessAspNetCoreCollection))]
public sealed class ProcessExecutionEndpointTests
{
    private readonly HttpClient _client;

    public ProcessExecutionEndpointTests(ProcessAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task PostExecute_WhenAllDependentStepsAreProvided_CompletesAvailableSteps()
    {
        var request =
            new ExecuteProcessRequest
            {
                Steps =
                [
                    CreateStep(RuntimeStepNames.Root),
                    CreateStep(RuntimeStepNames.StepA),
                    CreateStep(RuntimeStepNames.StepB),
                    CreateStep(RuntimeStepNames.Merge)
                ]
            };

        var response =
            await PostWithProcessIdAsync("/kaleido/processes/execute", request, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessExecutionResponse>();

        Assert.NotNull(contract);
        Assert.Contains(contract.Results, x => x.StepName == RuntimeStepNames.Root);
        Assert.Contains(contract.Results, x => x.StepName == RuntimeStepNames.StepA);
        Assert.Contains(contract.Results, x => x.StepName == RuntimeStepNames.StepB);
        Assert.Contains(contract.Results, x => x.StepName == RuntimeStepNames.Merge);
    }

    [Fact]
    public async Task PostExecute_WhenRequiredStepIsMissing_ReturnsAwaitingRequiredStep()
    {
        var request =
            new ExecuteProcessRequest
            {
                Steps =
                [
                    CreateStep(RuntimeStepNames.RequiredRoot)
                ]
            };

        var response =
            await PostWithProcessIdAsync("/kaleido/processes/execute", request, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessExecutionResponse>();

        Assert.NotNull(contract);
        Assert.NotNull(contract.RequiredStep);
        Assert.Equal(RuntimeStepNames.RequiredStep, contract.RequiredStep);
        Assert.Null(contract.TargetProcessorName);
        Assert.Empty(contract.AvailableSteps);
        Assert.Contains(contract.Results, x => x.StepName == RuntimeStepNames.RequiredRoot);
    }

    [Fact]
    public async Task PostExecute_WhenUnknownStepIsProvided_ReturnsValidationMessage()
    {
        var request =
            new ExecuteProcessRequest
            {
                Steps =
                [
                    CreateStep(RuntimeStepNames.Root),
                    CreateStep("TotallyFakeStep")
                ]
            };

        var response =
            await PostWithProcessIdAsync("/kaleido/processes/execute", request, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessExecutionResponse>();

        Assert.NotNull(contract);
        Assert.Contains(
            contract.Results,
            x => x.StepName == "TotallyFakeStep"
                 && x.Messages.Any(m => m.Code == "UnknownStep"));
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

    private static ProcessStepRequest CreateStep(string stepName) =>
        new()
        {
            StepName = stepName,
            Request = System.Text.Json.JsonSerializer.SerializeToElement(new { })
        };
}
