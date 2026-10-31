using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Kaleido.Eventing;
using Kaleido.Processor.Context;
using Kaleido.Processor.Eventing;
using Kaleido.Provider.SQLite;
using Kaleido.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Persistence;

// What happens when a step's handler ran but its outcome cannot be saved (#537, #513), through a real
// host on the in-memory and SQLite stores: the store refuses saves while the test says so.
public sealed class PersistenceFailureTests : IAsyncLifetime
{
    private const string ServiceName = "persistence";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-persistence-{Guid.NewGuid():N}.db");
    private readonly PersistenceCounter _counter = new();
    private readonly RefusedSaves _refusedSaves = new();
    private readonly RecordingPublisher _publisher = new();
    private IHost? _host;
    private HttpClient _client = null!;

    public static TheoryData<string> Stores => ["memory", "sqlite"];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            _host.Dispose();
        }

        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    // The step ran; its outcome is kept and saved once the store is back. Until then nothing runs on the
    // process, so the handler is never run twice; afterwards the state and the event are there, once.
    [Theory]
    [MemberData(nameof(Stores))]
    public async Task AnOutcomeThatCannotBeSaved_IsRecovered_AndTheStepIsNeverRunTwice(string store)
    {
        await StartHostAsync(store);
        var processId = await StartAsync();
        _refusedSaves.On = true;

        var (status, refused) = await PostStepAsync(PersistenceSteps.Record, processId);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal(ProcessorErrorCodes.OutcomePendingRecovery, ErrorCode(refused));
        Assert.Equal(1, _counter.Recorded);

        var (resentStatus, resent) = await PostStepAsync(PersistenceSteps.Record, processId);
        Assert.Equal(HttpStatusCode.Conflict, resentStatus);
        Assert.Equal(ProcessorErrorCodes.ProcessRecovering, ErrorCode(resent));
        Assert.Equal(1, _counter.Recorded);

        _refusedSaves.On = false;

        await WaitUntilAsync(async () => StepStatus(await StateAsync(processId), PersistenceSteps.Record) == "completed", "The outcome was not recovered in time.");
        await WaitUntilAsync(() => Task.FromResult(_publisher.Completed(processId, PersistenceSteps.Record) > 0), "The step-completed event was not delivered in time.");
        Assert.Equal(1, _publisher.Completed(processId, PersistenceSteps.Record));

        var (againStatus, _) = await PostStepAsync(PersistenceSteps.Record, processId);
        Assert.Equal(HttpStatusCode.OK, againStatus);
        Assert.Equal(2, _counter.Recorded);
    }

    // A retry with the same Idempotency-Key is told the outcome is unknown: never a second run.
    [Theory]
    [MemberData(nameof(Stores))]
    public async Task AKeyedRetryOfAnOutcomeThatCannotBeSaved_IsNotRunAgain(string store)
    {
        await StartHostAsync(store);
        var processId = await StartAsync();
        _refusedSaves.On = true;

        var (status, _) = await PostStepAsync(PersistenceSteps.Record, processId, idempotencyKey: "close-1-7f3a9c2e5b1d");
        var (retryStatus, retry) = await PostStepAsync(PersistenceSteps.Record, processId, idempotencyKey: "close-1-7f3a9c2e5b1d");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal(HttpStatusCode.Conflict, retryStatus);
        Assert.Equal(ProcessorErrorCodes.IdempotencyOutcomeUnknown, ErrorCode(retry));
        Assert.Equal(1, _counter.Recorded);
    }

    // #513: on SQLite the database refuses the step's event inside the transition, so it rolls back.
    // The rolled-back DbContext used to keep the process at the version it was not saved at; now the
    // outcome is kept and recovered in a new scope, and the event is saved with it.
    [Fact]
    public async Task OnSqlite_ARefusedEvent_RollsTheTransitionBack_AndTheOutcomeIsRecoveredWithItsEvent()
    {
        await StartHostAsync("sqlite");
        await using (var scope = _host!.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database;
            await db.ExecuteSqlRawAsync("CREATE TABLE TestSwitch (RefuseEvents INTEGER NOT NULL); INSERT INTO TestSwitch VALUES (0);", Cancel);
            await db.ExecuteSqlRawAsync("CREATE TRIGGER refuse_events BEFORE INSERT ON OutboxEvents WHEN (SELECT RefuseEvents FROM TestSwitch) = 1 BEGIN SELECT RAISE(ABORT, 'events refused'); END;", Cancel);
        }

        var processId = await StartAsync();
        await RefuseEventsAsync(true);

        var (status, refused) = await PostStepAsync(PersistenceSteps.Record, processId);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Equal(ProcessorErrorCodes.OutcomePendingRecovery, ErrorCode(refused));
        Assert.NotEqual("completed", StepStatus(await StateAsync(processId), PersistenceSteps.Record));

        await RefuseEventsAsync(false);

        await WaitUntilAsync(async () => StepStatus(await StateAsync(processId), PersistenceSteps.Record) == "completed", "The outcome was not recovered in time.");
        await WaitUntilAsync(() => Task.FromResult(_publisher.Completed(processId, PersistenceSteps.Record) > 0), "The step-completed event was not delivered in time.");
        Assert.Equal(1, _counter.Recorded);
    }

    private async Task StartHostAsync(string store)
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(_counter);
                    var kaleido = services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = ServiceName,
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(PersistenceFailureTests).Assembly],
                        TypeFilter = type => type.Namespace == typeof(PersistenceFailureTests).Namespace,
                        Events = new() { Resilience = new() { RetryDelay = TimeSpan.FromMilliseconds(50) } },
                    })
                        .AddInMemoryStores()
                        .AddHttp(new() { IncludeFrameworkMessages = true });

                    if (store == "sqlite")
                    {
                        kaleido.AddSqliteContextStore($"Data Source={_databasePath}").AddSqliteOutboxStore($"Data Source={_databasePath}").AddSqliteIdempotencyStore($"Data Source={_databasePath}");
                    }

                    RefuseSavesOnDemand(services);

                    // A publisher, so steps write their events to the outbox.
                    services.RemoveAll<IEventPublisher>();
                    services.AddSingleton<IEventPublisher>(_publisher);
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

        if (store == "sqlite")
        {
            await using var scope = _host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
        }

        _client = _host.GetTestClient();
    }

    // Wraps whichever context store is registered, so its saves fail while RefusedSaves is on.
    private void RefuseSavesOnDemand(IServiceCollection services)
    {
        var registered = services.Last(x => x.ServiceType == typeof(IKaleidoContextStore));
        services.Remove(registered);
        services.Add(new ServiceDescriptor(
            typeof(IKaleidoContextStore),
            provider => new RefusingContextStore(
                (IKaleidoContextStore)(registered.ImplementationInstance
                    ?? registered.ImplementationFactory?.Invoke(provider)
                    ?? ActivatorUtilities.CreateInstance(provider, registered.ImplementationType!)),
                _refusedSaves),
            registered.Lifetime));
    }

    private async Task RefuseEventsAsync(bool refuse)
    {
        await using var scope = _host!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database
            .ExecuteSqlRawAsync(refuse ? "UPDATE TestSwitch SET RefuseEvents = 1;" : "UPDATE TestSwitch SET RefuseEvents = 0;", Cancel);
    }

    private async Task<Guid> StartAsync()
    {
        var (status, body) = await PostStepAsync(PersistenceSteps.Start, processId: null);
        Assert.True(status == HttpStatusCode.OK, body.ToString());
        return body.GetProperty("processId").GetGuid();
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> PostStepAsync(string step, Guid? processId, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/{ServiceName}/processes/steps/{step.ToLowerInvariant()}")
        {
            Content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json"),
        };

        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation(KaleidoIdempotencyHeaders.IdempotencyKey, idempotencyKey);
        }

        using var response = await _client.SendAsync(request, Cancel);
        return (response.StatusCode, Parse(await response.Content.ReadAsStringAsync(Cancel)));
    }

    private async Task<JsonElement> StateAsync(Guid processId)
    {
        using var response = await _client.GetAsync($"/{ServiceName}/processes/{processId}", Cancel);
        var body = await response.Content.ReadAsStringAsync(Cancel);
        Assert.True(response.IsSuccessStatusCode, $"State read failed ({(int)response.StatusCode}): {body}");
        return Parse(body);
    }

    private static string? ErrorCode(JsonElement body) => body.GetProperty("errors")[0].GetProperty("code").GetString();

    private static string? StepStatus(JsonElement state, string step) =>
        state.GetProperty("steps").EnumerateArray()
            .Where(x => x.GetProperty("stepName").GetString() == step)
            .Select(x => x.GetProperty("status").GetString())
            .SingleOrDefault();

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, string failure)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, failure);
            await Task.Delay(100, Cancel);
        }
    }

    private sealed class RefusedSaves
    {
        private volatile bool _on;

        public bool On
        {
            get => _on;
            set => _on = value;
        }
    }

    private sealed class RefusingContextStore(IKaleidoContextStore inner, RefusedSaves refused) : IKaleidoContextStore
    {
        public Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken) =>
            inner.LoadAsync(processId, cancellationToken);

        public Task<ProcessorContext> SaveAsync(ProcessorContext context, CancellationToken cancellationToken) =>
            refused.On
                ? throw new TimeoutException("The store is not available.")
                : inner.SaveAsync(context, cancellationToken);
    }

    // Counts the step-completed events the relay hands over.
    private sealed class RecordingPublisher : IEventPublisher
    {
        private readonly ConcurrentQueue<(Guid ProcessId, string StepName)> _completed = new();

        public int Completed(Guid processId, string stepName) => _completed.Count(x => x.ProcessId == processId && x.StepName == stepName);

        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent
        {
            if (envelope.Event is StepCompleted completed && envelope.Context is ProcessEventContext context)
            {
                _completed.Enqueue((context.ProcessId, completed.StepName));
            }

            return Task.CompletedTask;
        }
    }
}

public static class PersistenceSteps
{
    public const string Start = nameof(PersistenceStartStep);
    public const string Record = nameof(PersistenceRecordStep);
}

public sealed class PersistenceCounter
{
    private int _recorded;

    public int Recorded => Volatile.Read(ref _recorded);

    public void Record() => Interlocked.Increment(ref _recorded);
}

[ProcessStep(DisplayName = "Start", Description = "Starts the case.")]
public sealed record PersistenceStartStep : IProcessStep;

[ProcessStep(DisplayName = "Record", Description = "Records something outside Kaleido (counted), then completes.")]
[DependsOn<PersistenceStartStep>]
[Repeatable]
public sealed record PersistenceRecordStep : IProcessStep;

public sealed class PersistenceStartHandler : IProcessStepHandler<PersistenceStartStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(PersistenceStartStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(ProcessStepHandlerResult.Success());
}

public sealed class PersistenceRecordHandler(PersistenceCounter counter) : IProcessStepHandler<PersistenceRecordStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(PersistenceRecordStep processStep, ProcessStepContext context, CancellationToken cancellationToken)
    {
        counter.Record();
        return Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
