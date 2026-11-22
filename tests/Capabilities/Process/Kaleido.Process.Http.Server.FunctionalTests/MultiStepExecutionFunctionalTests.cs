using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Provider.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Server.FunctionalTests.MultiStep;

public sealed class MultiStepExecutionFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task DependentSteps_SubmittedTogether_AllComplete()
    {
        await using var app = await StartAsync();
        using var response = await PostAsync(app, Step(nameof(RootStep)), Step(nameof(FirstStep)), Step(nameof(SecondStep)), Step(nameof(MergeStep)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal([nameof(RootStep), nameof(FirstStep), nameof(SecondStep), nameof(MergeStep)],
            result.Steps.Select(step => step.StepName));
        Assert.All(result.Steps, step => Assert.Equal(StepExecutionStatus.Completed, step.ExecutionStatus));
    }

    [Fact]
    public async Task ARequiredStep_NotSubmittedYet_IsReturnedByName()
    {
        await using var app = await StartAsync();
        using var response = await PostAsync(app, Step(nameof(RequiredRootStep)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(ProcessExecutionState.AwaitingRequiredStep, result.State);
        Assert.Equal(nameof(RequiredNextStep), result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Empty(result.AvailableSteps);
        Assert.Equal(nameof(RequiredRootStep), Assert.Single(result.Steps).StepName);
    }

    [Fact]
    public async Task AnUnknownStep_RejectsTheWholeRequestBeforeCreatingAProcess()
    {
        await using var app = await StartAsync();
        using var response = await PostAsync(app, Step(nameof(RootStep)), Step("TotallyFakeStep"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.ProcessId));
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel);
        var detail = Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors);
        Assert.Equal(ProcessorErrorCodes.UnknownStep, detail.Code);
        Assert.Contains("TotallyFakeStep", detail.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"itemId\":\"not-a-guid\"}", "itemId")]
    [InlineData("{\"on\":\"2026-10-15\"}", "itemId")]
    public async Task InvalidTypedInput_RejectsTheWholeRequestBeforeCreatingAProcess(string input, string field)
    {
        await using var app = await StartAsync();
        using var document = JsonDocument.Parse(input);
        using var response = await PostAsync(app, new ProcessStepRequest { StepName = nameof(InputStep), Request = document.RootElement.Clone() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains(KaleidoCorrelationHeaders.ProcessId));
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel);
        var detail = Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors);
        Assert.Equal(KaleidoErrorCodes.ArgumentError, detail.Code);
        Assert.Contains(nameof(InputStep), detail.Message, StringComparison.Ordinal);
        Assert.Contains(field, detail.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidTypedInput_IsConstructedBeforeTheHandlerRuns()
    {
        await using var app = await StartAsync();
        var itemId = Guid.NewGuid();
        using var response = await PostAsync(app, new ProcessStepRequest
        {
            StepName = nameof(InputStep),
            Request = JsonSerializer.SerializeToElement(new InputStep { ItemId = itemId, On = new DateOnly(2026, 10, 15) }, KaleidoJsonOptions.Options),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(nameof(InputStep), Assert.Single(result.Steps).StepName);
        Assert.Equal(itemId, app.Services.GetRequiredService<ReceivedInput>().ItemId);
    }

    [Fact]
    public async Task ABusinessFailure_IsInTheStepResultDespiteHttp200()
    {
        await using var app = await StartAsync();
        using var response = await PostAsync(app, Step(nameof(FailingStep)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        var failed = Assert.Single(result.Steps);
        Assert.Equal(StepExecutionStatus.Failed, failed.ExecutionStatus);
        Assert.Contains(failed.BusinessMessages, message => message.Code == "step_failed" && message.Type == MessageType.Error);
    }

    [Fact]
    public async Task AStepNamedTwiceWithDifferentCasing_RejectsTheRequest()
    {
        await using var app = await StartAsync();
        using var response = await PostAsync(app, Step(nameof(RootStep)), Step(nameof(RootStep).ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel);
        var detail = Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors);
        Assert.Equal(ProcessorErrorCodes.InvalidRequest, detail.Code);
        Assert.Contains(nameof(RootStep).ToUpperInvariant(), detail.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task APerStepEnvelope_CreatesOrResumesTheNamedProcess(bool resume)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        Guid? original = null;
        if (resume)
        {
            using var seeded = await PostStepAsync(client, nameof(SeedStep));
            Assert.Equal(HttpStatusCode.OK, seeded.StatusCode);
            original = Assert.IsType<ProcessResult>(await seeded.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel)).ProcessId;
        }

        using var response = await PostStepAsync(client, nameof(RootStep), original);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.NotEqual(Guid.Empty, result.ProcessId);
        if (original is { } id)
        {
            Assert.Equal(id, result.ProcessId);
        }

        Assert.Equal(result.ProcessId.ToString("D"), Assert.Single(response.Headers.GetValues(KaleidoCorrelationHeaders.ProcessId)));
        var executed = Assert.Single(result.Steps);
        Assert.Equal(nameof(RootStep), executed.StepName);
        Assert.Equal(StepExecutionStatus.Completed, executed.ExecutionStatus);
        Assert.Empty(executed.BusinessMessages);
        if (resume)
        {
            var state = await client.GetFromJsonAsync<ProcessStateSnapshot>($"/execution/1/processes/{result.ProcessId:D}", KaleidoJsonOptions.Options, Cancel);
            var snapshot = Assert.IsType<ProcessStateSnapshot>(state);
            Assert.Contains(snapshot.Steps, step => step.StepName == nameof(SeedStep) && step.Status == StepExecutionStatus.Completed);
            Assert.Contains(snapshot.Steps, step => step.StepName == nameof(RootStep) && step.Status == StepExecutionStatus.Completed);
            Assert.Contains(nameof(SecondStep), snapshot.AvailableSteps);
        }
    }

    [Fact]
    public async Task APerStepRequestForAnUnknownProcess_Answers400AndDoesNotCreateIt()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var missing = Guid.NewGuid();
        using var response = await PostStepAsync(client, nameof(RootStep), missing);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(ProcessorErrorCodes.ProcessNotFound, Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors).Code);
        using var state = await client.GetAsync($"/execution/1/processes/{missing:D}", Cancel);
        Assert.Equal(HttpStatusCode.NotFound, state.StatusCode);
    }

    [Theory]
    [InlineData("""{"processStep":{"on":"2026-10-15"}}""", "itemId")]
    [InlineData("""{"processStep":{"itemId":"6f1c2a9e-5d3b-4f7a-9c1e-2b8d4e6a0f13","on":"tomorrow"}}""", "$.processStep.on")]
    public async Task APerStepRequestWithInvalidTypedInput_Answers400NamingItsPath(string body, string field)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await PostStepAsync(client, nameof(InputStep), body: body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel);
        var detail = Assert.Single(Assert.IsType<KaleidoErrorResponse>(error).Errors);
        Assert.Equal(KaleidoErrorCodes.ArgumentError, detail.Code);
        Assert.Contains(field, detail.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task APerStepRequest_ReturnsTheHandlersTypedResult()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        var itemId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new ExecuteStepRequest<InputStep>
        {
            ProcessStep = new InputStep { ItemId = itemId, On = new DateOnly(2026, 10, 15) },
        }, KaleidoJsonOptions.Options);
        using var response = await PostStepAsync(client, nameof(InputStep), body: body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(itemId, Assert.IsType<JsonElement>(Assert.Single(result.Steps).Response).GetProperty("itemId").GetGuid());
    }

    [Fact]
    public async Task APerStepRequestWithNonJsonContent_Answers415()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestClient();
        using var response = await PostStepAsync(client, nameof(InputStep), body: "itemId=1", contentType: "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    private static ProcessStepRequest Step(string name) => new()
    {
        StepName = name,
        Request = JsonSerializer.SerializeToElement(new { }, KaleidoJsonOptions.Options),
    };

    private static async Task<HttpResponseMessage> PostAsync(WebApplication app, params ProcessStepRequest[] steps)
    {
        using var client = app.GetTestClient();
        return await client.PostAsJsonAsync("/execution/1/processes/execute", new ExecuteProcessRequest { Steps = steps }, KaleidoJsonOptions.Options, Cancel);
    }

    private static async Task<HttpResponseMessage> PostStepAsync(HttpClient client, string name, Guid? processId = null,
        string body = "{\"processStep\":{}}", string contentType = "application/json")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/execution/1/processes/steps/{name.ToLowerInvariant()}")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, contentType),
        };
        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString("D"));
        }

        return await client.SendAsync(request, Cancel);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ReceivedInput>();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "execution",
            ServiceVersion = "1",
            Assemblies = [typeof(MultiStepExecutionFunctionalTests).Assembly],
            TypeFilter = type => type.Namespace == typeof(MultiStepExecutionFunctionalTests).Namespace,
        })
            .AddEventing()
            .AddProcess()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddProcessHttpServer()
            .AddInMemoryProviders();
        var app = builder.Build();
        app.MapProcessor();
        await app.StartAsync(Cancel);
        return app;
    }

    public sealed class ReceivedInput
    {
        public Guid ItemId { get; private set; }
        public void Record(Guid itemId) => ItemId = itemId;
    }

    [ProcessStep(DisplayName = "Root", Description = "Starts the dependency graph")]
    public sealed record RootStep : IProcessStep;

    [ProcessStep(DisplayName = "First", Description = "Runs after the root")]
    [DependsOn<RootStep>]
    public sealed record FirstStep : IProcessStep;

    [ProcessStep(DisplayName = "Second", Description = "Runs after the root")]
    [DependsOn<RootStep>]
    public sealed record SecondStep : IProcessStep;

    [ProcessStep(DisplayName = "Merge", Description = "Runs after both dependent steps")]
    [DependsOn<FirstStep>]
    [DependsOn<SecondStep>]
    public sealed record MergeStep : IProcessStep;

    [ProcessStep(DisplayName = "Required root", Description = "Requires a continuation step")]
    public sealed record RequiredRootStep : IProcessStep;

    public sealed record RequiredRootResult;

    [ProcessStep(DisplayName = "Required next", Description = "Continues the required step")]
    [DependsOn<RequiredRootStep>]
    public sealed record RequiredNextStep : IProcessStep;

    [ProcessStep(DisplayName = "Input", Description = "Receives typed input")]
    public sealed record InputStep : IProcessStep
    {
        [Required]
        [Description("The item id")]
        public required Guid ItemId { get; init; }
        public DateOnly On { get; init; }
    }

    public sealed record InputResponse(Guid ItemId);

    [ProcessStep(DisplayName = "Seed", Description = "Creates a process before another step")]
    public sealed record SeedStep : IProcessStep;

    [ProcessStep(DisplayName = "Fail", Description = "Returns a business failure")]
    public sealed record FailingStep : IProcessStep;

    public sealed class RootHandler : IProcessStepHandler<RootStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(RootStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class FirstHandler : IProcessStepHandler<FirstStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(FirstStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class SecondHandler : IProcessStepHandler<SecondStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(SecondStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class MergeHandler : IProcessStepHandler<MergeStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(MergeStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class RequiredRootHandler : IProcessStepHandler<RequiredRootStep, RequiredRootResult>
    {
        public Task<ProcessStepHandlerResult<RequiredRootResult>> ExecuteAsync(RequiredRootStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success(new RequiredRootResult()).Next<RequiredNextStep>());
    }

    public sealed class RequiredNextHandler : IProcessStepHandler<RequiredNextStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(RequiredNextStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class InputHandler(ReceivedInput received) : IProcessStepHandler<InputStep, InputResponse>
    {
        public Task<ProcessStepHandlerResult<InputResponse>> ExecuteAsync(InputStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            received.Record(step.ItemId);
            return Task.FromResult(ProcessStepHandlerResult.Success(new InputResponse(step.ItemId)));
        }
    }

    public sealed class SeedHandler : IProcessStepHandler<SeedStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(SeedStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class FailingHandler : IProcessStepHandler<FailingStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(FailingStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Failure(new ProcessMessage { Code = "step_failed", Type = MessageType.Error, Message = "The step failed" }));
    }
}
