using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Process.Eventing;
using Kaleido.Provider.InMemory;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Server.FunctionalTests.Cancellation;

public sealed class CancellationFinalizationFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledBeforeTheHandler_DoesNotInvokeIt(bool sqlite)
    {
        await using var host = await CancellationHost.StartAsync(sqlite);
        var processId = await host.StartProcessAsync();
        host.Cancellation.CancelBeforeHandling = true;

        using (await host.PostStepAsync(nameof(CancelAfterStep), processId))
        {
        }
        host.Cancellation.CancelBeforeHandling = false;

        var state = await host.StateAsync(processId);
        Assert.Equal(0, host.Cancellation.Invocations);
        Assert.DoesNotContain(state.Steps, step => step.StepName == nameof(CancelAfterStep) && step.Status == StepExecutionStatus.Completed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledWhileTheHandlerRuns_RecordsCancellationWithoutAdvancingTheProcess(bool sqlite)
    {
        await using var host = await CancellationHost.StartAsync(sqlite);
        var processId = await host.StartProcessAsync();
        var before = await host.StateAsync(processId);

        using (await host.PostStepAsync(nameof(CancelDuringStep), processId))
        {
        }

        var after = await host.StateAsync(processId);
        Assert.Equal(1, host.Cancellation.Invocations);
        Assert.Equal(StepExecutionStatus.Canceled, Assert.Single(after.Steps, step => step.StepName == nameof(CancelDuringStep)).Status);
        Assert.Equal(before.State, after.State);
        Assert.Equal(before.AvailableSteps, after.AvailableSteps);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancelledAfterTheHandlerReturns_SavesItsCompletionAndPublishesTheEvent(bool sqlite)
    {
        await using var host = await CancellationHost.StartAsync(sqlite);
        var processId = await host.StartProcessAsync();

        using (await host.PostStepAsync(nameof(CancelAfterStep), processId))
        {
        }

        Assert.Equal(1, host.Cancellation.Invocations);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single((await host.StateAsync(processId)).Steps,
            step => step.StepName == nameof(CancelAfterStep)).Status);
        await host.Publisher.WaitForAsync(processId, nameof(CancelAfterStep)).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AKeyedCallCancelledAsItsHandlerReturns_ReplaysTheSavedAnswerWithoutRunningAgain(bool sqlite)
    {
        await using var host = await CancellationHost.StartAsync(sqlite);
        var processId = await host.StartProcessAsync();
        const string key = "cancelled-call-0a1b2c3d4e5f";
        using var first = await host.PostStepAsync(nameof(CancelAfterStep), processId, key);
        Assert.Equal(1, host.Cancellation.Invocations);
        using var replay = await host.PostStepAsync(nameof(CancelAfterStep), processId, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues(KaleidoIdempotencyHeaders.Replayed)));
        Assert.Equal(1, host.Cancellation.Invocations);
        var result = Assert.IsType<ProcessResult>(await replay.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(processId, result.ProcessId);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(result.Steps).ExecutionStatus);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single((await host.StateAsync(processId)).Steps, step => step.StepName == nameof(CancelAfterStep)).Status);
    }

    private sealed class CancellationHost(WebApplication app, CancellationControl cancellation, RecordingPublisher publisher,
        string? databasePath) : IAsyncDisposable
    {
        public CancellationControl Cancellation { get; } = cancellation;
        public RecordingPublisher Publisher { get; } = publisher;

        public static async Task<CancellationHost> StartAsync(bool sqlite)
        {
            var path = sqlite ? Path.Combine(Path.GetTempPath(), $"kaleido-cancel-{Guid.NewGuid():N}.db") : null;
            var cancellation = new CancellationControl();
            var publisher = new RecordingPublisher();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(cancellation);
            builder.Services.AddSingleton(publisher);
            var kaleido = builder.Services.AddKaleido(builder.Configuration, new()
            {
                ServiceName = "cancellation",
                ServiceVersion = "1",
                Assemblies = [typeof(CancellationFinalizationFunctionalTests).Assembly],
                TypeFilter = type => type.Namespace == typeof(CancellationFinalizationFunctionalTests).Namespace,
            })
                .AddEventing()
                .AddProcess()
                .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
                .AddHttp()
                .AddProcessHttpServer();
            if (path is null)
            {
                kaleido.AddInMemoryProviders();
            }
            else
            {
                var connection = $"Data Source={path};Pooling=False";
                kaleido.AddSqliteContextProvider(connection).AddSqliteOutboxProvider(connection).AddSqliteIdempotencyProvider(connection);
            }

            kaleido.AddOutboxRelay<RecordingPublisher>();
            var app = builder.Build();
            app.Use((context, next) =>
            {
                context.RequestAborted = cancellation.Begin();
                return next(context);
            });
            app.MapProcessor();
            if (path is not null)
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
            }

            await app.StartAsync(Cancel);
            return new CancellationHost(app, cancellation, publisher, path);
        }

        public async Task<Guid> StartProcessAsync()
        {
            using var response = await PostStepAsync(nameof(CancellationStartStep), processId: null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel);
            return Assert.IsType<ProcessResult>(result).ProcessId;
        }

        public async Task<HttpResponseMessage> PostStepAsync(string name, Guid? processId, string? key = null)
        {
            using var client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/cancellation/1/processes/steps/{name.ToLowerInvariant()}")
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

        public async Task<ProcessStateSnapshot> StateAsync(Guid processId)
        {
            using var client = app.GetTestClient();
            var state = await client.GetFromJsonAsync<ProcessStateSnapshot>($"/cancellation/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel);
            return Assert.IsType<ProcessStateSnapshot>(state);
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
            Cancellation.Dispose();
            if (databasePath is not null)
            {
                SqliteConnection.ClearAllPools();
                File.Delete(databasePath);
            }
        }
    }

    public sealed class CancellationControl : IDisposable
    {
        private CancellationTokenSource current = new();
        private int invocations;
        public bool CancelBeforeHandling { get; set; }
        public int Invocations => Volatile.Read(ref invocations);
        public CancellationToken Begin()
        {
            current.Dispose();
            current = new CancellationTokenSource();
            if (CancelBeforeHandling)
            {
                current.Cancel();
            }

            return current.Token;
        }

        public Task CancelAsync()
        {
            Interlocked.Increment(ref invocations);
            return current.CancelAsync();
        }

        public void Dispose() => current.Dispose();
    }

    public sealed class RecordingPublisher : IEventPublisher
    {
        private readonly ConcurrentQueue<(Guid ProcessId, string StepName)> completed = new();
        private readonly ConcurrentDictionary<(Guid ProcessId, string StepName), TaskCompletionSource> waiting = new();
        public Task WaitForAsync(Guid processId, string stepName)
        {
            var key = (processId, stepName);
            if (completed.Contains(key))
            {
                return Task.CompletedTask;
            }

            var waiter = waiting.GetOrAdd(key, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            if (completed.Contains(key))
            {
                waiter.TrySetResult();
            }

            return waiter.Task;
        }

        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
            where TEvent : IKaleidoEvent
        {
            if (envelope.EventType == ProcessEventTypes.ProcessStepCompleted
                && envelope.Event is StepCompleted step && envelope.Context is ProcessEventContext context)
            {
                var key = (context.ProcessId, step.StepName);
                completed.Enqueue(key);
                if (waiting.TryGetValue(key, out var waiter))
                {
                    waiter.TrySetResult();
                }
            }

            return Task.CompletedTask;
        }
    }

    [ProcessStep(DisplayName = "Start cancellation case", Description = "Starts a process for cancellation tests")]
    public sealed record CancellationStartStep : IProcessStep;

    [ProcessStep(DisplayName = "Cancel during", Description = "Cancels a request during handler execution")]
    [DependsOn<CancellationStartStep>]
    public sealed record CancelDuringStep : IProcessStep;

    [ProcessStep(DisplayName = "Cancel after", Description = "Cancels a request after handler execution")]
    [DependsOn<CancellationStartStep>]
    public sealed record CancelAfterStep : IProcessStep;

    public sealed class CancellationStartHandler : IProcessStepHandler<CancellationStartStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(CancellationStartStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed class CancelDuringHandler(CancellationControl control) : IProcessStepHandler<CancelDuringStep>
    {
        public async Task<ProcessStepHandlerResult> ExecuteAsync(CancelDuringStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            await control.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return ProcessStepHandlerResult.Success();
        }
    }

    public sealed class CancelAfterHandler(CancellationControl control) : IProcessStepHandler<CancelAfterStep>
    {
        public async Task<ProcessStepHandlerResult> ExecuteAsync(CancelAfterStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            await control.CancelAsync();
            return ProcessStepHandlerResult.Success();
        }
    }
}
