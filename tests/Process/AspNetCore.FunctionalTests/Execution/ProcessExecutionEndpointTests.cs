using Kaleido.Process.AspNetCore.Contracts;
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
                ProcessId = Guid.NewGuid(),
                RequestId = "request-1",
                Steps =
                [
                    CreateStep(RuntimeStepNames.Root),
                    CreateStep(RuntimeStepNames.StepA),
                    CreateStep(RuntimeStepNames.StepB),
                    CreateStep(RuntimeStepNames.Merge)
                ]
            };

        var response =
            await _client.PostAsJsonAsync(
                "/kaleido/processes/execute",
                request);

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
                ProcessId = Guid.NewGuid(),
                RequestId = "request-2",
                Steps =
                [
                    CreateStep(RuntimeStepNames.RequiredRoot)
                ]
            };

        var response =
            await _client.PostAsJsonAsync(
                "/kaleido/processes/execute",
                request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessExecutionResponse>();

        Assert.NotNull(contract);
        Assert.Equal(RuntimeStepNames.RequiredStep, contract.RequiredStep);
        Assert.Empty(contract.AvailableSteps);
        Assert.Contains(contract.Results, x => x.StepName == RuntimeStepNames.RequiredRoot);
    }

    [Fact]
    public async Task PostExecute_WhenUnknownStepIsProvided_ReturnsValidationMessage()
    {
        var request =
            new ExecuteProcessRequest
            {
                ProcessId = Guid.NewGuid(),
                RequestId = "request-3",
                Steps =
                [
                    CreateStep(RuntimeStepNames.Root),
                    CreateStep("TotallyFakeStep")
                ]
            };

        var response =
            await _client.PostAsJsonAsync(
                "/kaleido/processes/execute",
                request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessExecutionResponse>();

        Assert.NotNull(contract);
        Assert.Contains(
            contract.Results.SelectMany(x => x.Messages),
            x => x.Code == "UnknownStep");
    }

    private static ProcessStepRequest CreateStep(
        string stepName) =>
        new()
        {
            StepName = stepName,
            Request = ProcessHttpJson.EmptyObject()
        };
}
