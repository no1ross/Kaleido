using System.Net;
using System.Net.Http.Json;
using Kaleido.Provider.InMemory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Server.FunctionalTests.Concurrent;

public sealed class ConcurrentProcessFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoRequestsForTheSameProcess_AfterOneHandlerRuns_TheStaleSaveConflictsWithoutReplayingIt(bool keyed)
    {
        await using var app = await StartAsync();
        var processId = await StartProcessAsync(app);
        var gate = app.Services.GetRequiredService<SlowGate>();
        const string key = "slow-save-conflict-0a1b2c3d4e5f";
        var slow = PostAsync(app, nameof(SlowStep), processId, keyed ? key : null);
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        using var quick = await PostAsync(app, nameof(QuickStep), processId);
        Assert.Equal(HttpStatusCode.OK, quick.StatusCode);
        var quickResult = Assert.IsType<ProcessResult>(await quick.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(quickResult.Steps).ExecutionStatus);
        gate.Release();
        using var conflict = await slow;
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var failure = Assert.IsType<KaleidoErrorResponse>(await conflict.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(ProcessorErrorCodes.OutcomeNotSaved, Assert.Single(failure.Errors).Code);
        Assert.Equal(1, gate.Invocations);
        using var client = app.GetTestClient();
        var state = Assert.IsType<ProcessStateSnapshot>(await client.GetFromJsonAsync<ProcessStateSnapshot>($"/concurrent/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel));
        Assert.Contains(state.Steps, step => step.StepName == nameof(QuickStep) && step.Status == StepExecutionStatus.Completed);
        Assert.DoesNotContain(state.Steps, step => step.StepName == nameof(SlowStep) && step.Status == StepExecutionStatus.Completed);
        if (keyed)
        {
            using var retry = await PostAsync(app, nameof(SlowStep), processId, key);
            Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
            var unknown = Assert.IsType<KaleidoErrorResponse>(await retry.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel));
            Assert.Equal(ProcessorErrorCodes.IdempotencyOutcomeUnknown, Assert.Single(unknown.Errors).Code);
            Assert.Equal(1, gate.Invocations);
        }
    }

    private static async Task<Guid> StartProcessAsync(WebApplication app)
    {
        using var response = await PostAsync(app, nameof(StartStep), null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel)).ProcessId;
    }

    private static async Task<HttpResponseMessage> PostAsync(WebApplication app, string name, Guid? processId, string? key = null)
    {
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/concurrent/1/processes/steps/{name.ToLowerInvariant()}")
        {
            Content = JsonContent.Create(new { processStep = new { } }, options: KaleidoJsonOptions.Options),
        };
        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString("D"));
        }
        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, key);
        }

        return await client.SendAsync(request, Cancel);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<SlowGate>();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "concurrent",
            ServiceVersion = "1",
            Assemblies = [typeof(ConcurrentProcessFunctionalTests).Assembly],
            TypeFilter = type => type.Namespace == typeof(ConcurrentProcessFunctionalTests).Namespace,
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

    public sealed class SlowGate
    {
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int invocations;
        public Task Entered => entered.Task;
        public int Invocations => Volatile.Read(ref invocations);
        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref invocations);
            entered.TrySetResult();
            await released.Task.WaitAsync(cancellationToken);
        }
        public void Release() => released.TrySetResult();
    }

    [ProcessStep(DisplayName = "Start", Description = "Starts a process for a concurrent write test")]
    public sealed record StartStep : IProcessStep;

    [ProcessStep(DisplayName = "Slow", Description = "Runs while another request saves the process")]
    [DependsOn<StartStep>]
    public sealed record SlowStep : IProcessStep;

    [ProcessStep(DisplayName = "Quick", Description = "Saves a process before the slow request finishes")]
    [DependsOn<StartStep>]
    public sealed record QuickStep : IProcessStep;

    public sealed class StartHandler : IProcessStepHandler<StartStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(StartStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class SlowHandler(SlowGate gate) : IProcessStepHandler<SlowStep>
    {
        public async Task<ProcessStepHandlerResult> ExecuteAsync(SlowStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            return ProcessStepHandlerResult.Success();
        }
    }

    public sealed class QuickHandler : IProcessStepHandler<QuickStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(QuickStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
