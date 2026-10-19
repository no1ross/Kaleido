using System.Net;
using Kaleido.Http.FunctionalTests.Processor.Fixtures;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Processor;

namespace Kaleido.Http.FunctionalTests.Processor.Execution;

[Collection(nameof(ProcessorAspNetCoreSuite))]
public sealed class ProcessExecutionEndpointTests(ProcessorAspNetCoreFixture fixture)
{

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
            await PostExecuteAsync(request);

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
            await PostExecuteAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessExecutionResponse>();

        Assert.NotNull(contract);
        Assert.NotNull(contract.RequiredStep);
        Assert.Equal(RuntimeStepNames.RequiredStep, contract.RequiredStep.Name);
        Assert.Null(contract.TargetProcessorName);
        Assert.Empty(contract.AvailableSteps);
        Assert.Contains(contract.Results, x => x.StepName == RuntimeStepNames.RequiredRoot);
    }

    // A step that cannot be built rejects the whole request before anything runs: no process is
    // created, so no process id comes back (#344).
    [Fact]
    public async Task PostExecute_WhenAStepIsUnknown_Returns400NamingIt_AndCreatesNoProcess()
    {
        var response = await PostExecuteAsync(new ExecuteProcessRequest
        {
            Steps = [CreateStep(RuntimeStepNames.Root), CreateStep("TotallyFakeStep")],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.ProcessId));
        var error = Assert.Single((await response.Content.ReadAsync<KaleidoErrorResponse>())!.Errors);
        Assert.Equal(ProcessorErrorCodes.UnknownStep, error.Code);
        Assert.Contains("'TotallyFakeStep'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"itemId\":\"not-a-guid\"}", "itemId")]
    [InlineData("{\"on\":\"2026-10-15\"}", "itemId")]
    public async Task PostExecute_WhenAStepsInputDoesNotFit_Returns400NamingTheStepAndField_AndCreatesNoProcess(string input, string field)
    {
        using var json = System.Text.Json.JsonDocument.Parse(input);

        var response = await PostExecuteAsync(new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest { StepName = nameof(RuntimeInputStep), Request = json.RootElement.Clone() }],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.ProcessId));
        var error = Assert.Single((await response.Content.ReadAsync<KaleidoErrorResponse>())!.Errors);
        Assert.Equal(KaleidoErrorCodes.ArgumentError, error.Code);
        Assert.Contains($"Step '{nameof(RuntimeInputStep)}'", error.Message, StringComparison.Ordinal);
        Assert.Contains(field, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostExecute_AValidStepInput_IsBuiltByTheTransport_AndRuns()
    {
        using var json = System.Text.Json.JsonDocument.Parse($"{{\"itemId\":\"{Guid.NewGuid()}\",\"on\":\"2026-10-15\"}}");

        var response = await PostExecuteAsync(new ExecuteProcessRequest
        {
            Steps = [new ProcessStepRequest { StepName = nameof(RuntimeInputStep), Request = json.RootElement.Clone() }],
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains(KaleidoCorrelationHeaders.ProcessId));
        var contract = await response.Content.ReadAsync<ProcessExecutionResponse>();
        Assert.Contains(contract!.Results, x => x.StepName == nameof(RuntimeInputStep));
    }

    [Fact]
    public async Task PostExecute_WhenStepFails_ReturnsErrorInResponse()
    {
        var request =
            new ExecuteProcessRequest
            {
                Steps =
                [
                    CreateStep(RuntimeStepNames.Failing)
                ]
            };

        var response =
            await PostExecuteAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessExecutionResponse>();

        Assert.NotNull(contract);

        var result =
            Assert.Single(
                contract.Results,
                x => x.StepName == RuntimeStepNames.Failing);

        Assert.Contains(
            result.BusinessMessages,
            x => x.Code == "RuntimeFailingFailed"
                 && x.Type == MessageType.Error);
    }

    [Fact]
    public async Task PostExecute_WhenAStepIsSubmittedTwice_Returns400NamingTheStep()
    {
        var request =
            new ExecuteProcessRequest
            {
                Steps =
                [
                    CreateStep(RuntimeStepNames.Root),
                    CreateStep(RuntimeStepNames.Root.ToUpperInvariant())
                ]
            };

        var response =
            await PostExecuteAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body =
            await response.Content.ReadAsync<KaleidoErrorResponse>();

        var error = Assert.Single(body!.Errors);
        Assert.Equal(ProcessorErrorCodes.InvalidRequest, error.Code);
        Assert.Contains(RuntimeStepNames.Root.ToUpperInvariant(), error.Message, StringComparison.Ordinal);
    }

    // No process id: each test starts a new process, whose id the runtime generates.
    private Task<HttpResponseMessage> PostExecuteAsync(ExecuteProcessRequest body) =>
        fixture.Client.PostAsJsonAsync("/kaleido/processes/execute", body, KaleidoJsonOptions.Options);

    private static ProcessStepRequest CreateStep(string stepName) =>
        new()
        {
            StepName = stepName,
            Request = System.Text.Json.JsonSerializer.SerializeToElement(new { })
        };
}
