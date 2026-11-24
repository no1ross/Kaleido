using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Process.Eventing;
using Kaleido.Provider.InMemory;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Process.Http.Server.FunctionalTests.Persistence;

public sealed class PersistenceFailureFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARefusedStateSave_RecoversTheOutcomeAndEventWithoutRunningTheStepTwice(bool sqlite)
    {
        await using var host = await PersistenceHost.StartAsync(sqlite);
        var processId = await host.StartProcessAsync();
        host.Refusal.On = true;

        using var refused = await host.PostStepAsync(nameof(RecordStep), processId);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal(ProcessorErrorCodes.OutcomePendingRecovery, await ErrorCodeAsync(refused));
        Assert.Equal(1, host.Runs.Count);
        using var pending = await host.PostStepAsync(nameof(RecordStep), processId);
        Assert.Equal(HttpStatusCode.Conflict, pending.StatusCode);
        Assert.Equal(ProcessorErrorCodes.ProcessRecovering, await ErrorCodeAsync(pending));
        Assert.Equal(1, host.Runs.Count);

        host.Refusal.On = false;
        await host.Clock.PollScheduled.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        for (var seconds = 0; seconds < 12 && !host.Refusal.Recovered.IsCompleted; seconds++)
        {
            host.Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await host.Refusal.Recovered.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        for (var seconds = 0; seconds < 12 && host.Publisher.Completed(processId, nameof(RecordStep)) == 0; seconds++)
        {
            host.Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await host.Publisher.WaitForAsync(processId, nameof(RecordStep)).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single((await host.StateAsync(processId)).Steps, step => step.StepName == nameof(RecordStep)).Status);
        Assert.Equal(1, host.Publisher.Completed(processId, nameof(RecordStep)));
        Assert.Equal(1, host.Runs.Count);
        using var repeated = await host.PostStepAsync(nameof(RecordStep), processId);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(2, host.Runs.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AKeyedRetryOfARefusedSave_DoesNotRunTheStepAgain(bool sqlite)
    {
        await using var host = await PersistenceHost.StartAsync(sqlite);
        var processId = await host.StartProcessAsync();
        host.Refusal.On = true;
        const string key = "record-pending-0a1b2c3d4e5f";

        using var refused = await host.PostStepAsync(nameof(RecordStep), processId, key);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal(ProcessorErrorCodes.OutcomePendingRecovery, await ErrorCodeAsync(refused));
        using var retried = await host.PostStepAsync(nameof(RecordStep), processId, key);
        Assert.Equal(HttpStatusCode.Conflict, retried.StatusCode);
        Assert.Equal(ProcessorErrorCodes.IdempotencyOutcomeUnknown, await ErrorCodeAsync(retried));
        Assert.Equal(1, host.Runs.Count);
    }

    [Fact]
    public async Task ARefusedSqliteOutboxEvent_RollsBackStateAndRecoversItWithTheEvent()
    {
        await using var host = await PersistenceHost.StartAsync(sqlite: true);
        var processId = await host.StartProcessAsync();
        await host.SetEventRefusalAsync(true);

        using var refused = await host.PostStepAsync(nameof(RecordStep), processId);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal(ProcessorErrorCodes.OutcomePendingRecovery, await ErrorCodeAsync(refused));
        Assert.DoesNotContain((await host.StateAsync(processId)).Steps, step => step.StepName == nameof(RecordStep) && step.Status == StepExecutionStatus.Completed);
        Assert.Equal(1, host.Runs.Count);

        await host.SetEventRefusalAsync(false);
        await host.Clock.PollScheduled.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        for (var seconds = 0; seconds < 12 && host.Publisher.Completed(processId, nameof(RecordStep)) == 0; seconds++)
        {
            host.Clock.Advance(TimeSpan.FromSeconds(1));
            await Task.Yield();
        }

        await host.Publisher.WaitForAsync(processId, nameof(RecordStep)).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single((await host.StateAsync(processId)).Steps, step => step.StepName == nameof(RecordStep)).Status);
        Assert.Equal(1, host.Publisher.Completed(processId, nameof(RecordStep)));
        Assert.Equal(1, host.Runs.Count);
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response)
    {
        var error = Assert.IsType<KaleidoErrorResponse>(await response.Content.ReadFromJsonAsync<KaleidoErrorResponse>(KaleidoJsonOptions.Options, Cancel));
        return Assert.Single(error.Errors).Code;
    }

    private sealed class PersistenceHost(WebApplication app, string? path, RecoveryClock clock, RefusalControl refusal,
        RecordingPublisher publisher, RecordRuns runs) : IAsyncDisposable
    {
        public RecoveryClock Clock { get; } = clock;
        public RefusalControl Refusal { get; } = refusal;
        public RecordingPublisher Publisher { get; } = publisher;
        public RecordRuns Runs { get; } = runs;

        public static async Task<PersistenceHost> StartAsync(bool sqlite)
        {
            var path = sqlite ? Path.Combine(Path.GetTempPath(), $"kaleido-recovery-{Guid.NewGuid():N}.db") : null;
            var clock = new RecoveryClock(DateTimeOffset.UtcNow.AddMinutes(1));
            var refusal = new RefusalControl();
            var publisher = new RecordingPublisher();
            var runs = new RecordRuns();
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.AddSingleton(refusal);
            builder.Services.AddSingleton(publisher);
            builder.Services.AddSingleton(runs);
            var kaleido = builder.Services.AddKaleido(builder.Configuration, new()
            {
                ServiceName = "recovery",
                ServiceVersion = "1",
                Assemblies = [typeof(PersistenceFailureFunctionalTests).Assembly],
                TypeFilter = type => type.Namespace == typeof(PersistenceFailureFunctionalTests).Namespace,
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

            DecorateStore(builder.Services, refusal);
            kaleido.AddOutboxRelay<RecordingPublisher>();
            var app = builder.Build();
            app.MapProcessor();
            if (path is not null)
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
                await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.ExecuteSqlRawAsync(
                    "CREATE TABLE TestSwitch (RefuseEvents INTEGER NOT NULL); INSERT INTO TestSwitch VALUES (0); CREATE TRIGGER refuse_events BEFORE INSERT ON OutboxEvents WHEN (SELECT RefuseEvents FROM TestSwitch) = 1 BEGIN SELECT RAISE(ABORT, 'events refused'); END;", Cancel);
            }

            await app.StartAsync(Cancel);
            return new PersistenceHost(app, path, clock, refusal, publisher, runs);
        }

        public async Task<Guid> StartProcessAsync()
        {
            using var response = await PostStepAsync(nameof(StartStep), null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var processId = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel)).ProcessId;
            await Publisher.WaitForAsync(processId, nameof(StartStep)).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
            return processId;
        }

        public async Task<HttpResponseMessage> PostStepAsync(string name, Guid? processId, string? key = null)
        {
            using var client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/recovery/1/processes/steps/{name.ToLowerInvariant()}")
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
            return Assert.IsType<ProcessStateSnapshot>(await client.GetFromJsonAsync<ProcessStateSnapshot>($"/recovery/1/processes/{processId:D}", KaleidoJsonOptions.Options, Cancel));
        }

        public async Task SetEventRefusalAsync(bool on)
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.ExecuteSqlRawAsync(
                on ? "UPDATE TestSwitch SET RefuseEvents = 1;" : "UPDATE TestSwitch SET RefuseEvents = 0;", Cancel);
        }

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
            if (path is not null)
            {
                SqliteConnection.ClearAllPools();
                File.Delete(path);
            }
        }

        private static void DecorateStore(IServiceCollection services, RefusalControl refusal)
        {
            var registered = services.Last(item => item.ServiceType == typeof(IProcessContextStore));
            services.Remove(registered);
            services.Add(ServiceDescriptor.Describe(typeof(IProcessContextStore), provider => new RefusingProcessContextStore(
                (IProcessContextStore)(registered.ImplementationInstance
                    ?? registered.ImplementationFactory?.Invoke(provider)
                    ?? ActivatorUtilities.CreateInstance(provider, registered.ImplementationType ?? throw new InvalidOperationException("Missing context-store implementation."))),
                refusal), registered.Lifetime));
        }
    }

    private sealed class RecoveryClock(DateTimeOffset start) : FakeTimeProvider(start)
    {
        private readonly TaskCompletionSource scheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task PollScheduled => scheduled.Task;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = base.CreateTimer(callback, state, dueTime, period);
            if (period == Timeout.InfiniteTimeSpan && dueTime == TimeSpan.FromSeconds(1))
            {
                scheduled.TrySetResult();
            }

            return timer;
        }
    }

    private sealed class RefusalControl
    {
        private volatile bool on;
        private readonly TaskCompletionSource recovered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool On { get => on; set => on = value; }
        public Task Recovered => recovered.Task;
        public void MarkRecovered() => recovered.TrySetResult();
    }

    private sealed class RefusingProcessContextStore(IProcessContextStore inner, RefusalControl refusal) : IProcessContextStore
    {
        public Task<ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken) => inner.LoadAsync(key, cancellationToken);

        public async Task<ProcessContext> SaveAsync(ProcessContext context, CancellationToken cancellationToken)
        {
            if (refusal.On)
            {
                throw new TimeoutException("The context store is unavailable.");
            }

            var saved = await inner.SaveAsync(context, cancellationToken);
            if (saved.Steps.Any(step => step.StepName == nameof(RecordStep) && step.Status == StepExecutionStatus.Completed))
            {
                refusal.MarkRecovered();
            }

            return saved;
        }
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        private readonly ConcurrentQueue<(Guid ProcessId, string StepName)> completed = new();
        private readonly ConcurrentDictionary<(Guid ProcessId, string StepName), TaskCompletionSource> waiting = new();
        public int Completed(Guid processId, string stepName) => completed.Count(entry => entry == (processId, stepName));
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
            if (envelope.EventType == ProcessEventTypes.ProcessStepCompleted && envelope.Event is StepCompleted step && envelope.Context is ProcessEventContext context)
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

    public sealed class RecordRuns
    {
        private int count;
        public int Count => Volatile.Read(ref count);
        public void Increment() => Interlocked.Increment(ref count);
    }

    [ProcessStep(DisplayName = "Start", Description = "Starts a process for the persistence recovery test")]
    public sealed record StartStep : IProcessStep;

    public sealed class StartHandler : IProcessStepHandler<StartStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(StartStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    [ProcessStep(DisplayName = "Record", Description = "Records a repeatable step after starting the process")]
    [DependsOn<StartStep>]
    [Repeatable]
    public sealed record RecordStep : IProcessStep;

    public sealed class RecordHandler(RecordRuns runs) : IProcessStepHandler<RecordStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(RecordStep step, ProcessStepContext context, CancellationToken cancellationToken)
        {
            runs.Increment();
            return Task.FromResult(ProcessStepHandlerResult.Success());
        }
    }
}
