using System.Net;
using System.Text;
using System.Text.Json;
using Kaleido.Eventing;
using Kaleido.Provider.SQLite;
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

// What a failure to store a step's outcome does on the SQLite store, through a real host: the database
// refuses the step's event (a trigger on OutboxEvents, switched on per test), so the transition rolls back.
public sealed class PersistenceFailureTests : IAsyncLifetime
{
    private const string ServiceName = "persistence";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-persistence-{Guid.NewGuid():N}.db");
    private readonly PersistenceCounter _counter = new();
    private IHost? _host;
    private HttpClient _client = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _host = await new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton(_counter);
                    services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = ServiceName,
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(PersistenceFailureTests).Assembly],
                        TypeFilter = type => type.Namespace == typeof(PersistenceFailureTests).Namespace,
                    })
                        .AddInMemoryStores()
                        .AddHttp(new() { IncludeFrameworkMessages = true })
                        .AddSqliteContextStore($"Data Source={_databasePath}")
                        .AddSqliteOutboxStore($"Data Source={_databasePath}")
                        .AddSqliteIdempotencyStore($"Data Source={_databasePath}");

                    // A publisher, so steps write their events to the outbox.
                    services.RemoveAll<IEventPublisher>();
                    services.AddSingleton<IEventPublisher, DiscardingPublisher>();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>();
        await db.Database.EnsureCreatedAsync(Cancel);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE TestSwitch (RefuseEvents INTEGER NOT NULL); INSERT INTO TestSwitch VALUES (0);", Cancel);
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER refuse_events BEFORE INSERT ON OutboxEvents WHEN (SELECT RefuseEvents FROM TestSwitch) = 1 BEGIN SELECT RAISE(ABORT, 'events refused'); END;", Cancel);

        _client = _host.GetTestClient();
    }

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

    // #513: the rolled-back transition left the tracked process at the version it was not saved at, so
    // the failure save that follows in the same scope was refused as a conflict (409) instead of
    // recording the step.
    [Fact]
    public async Task ARefusedTransition_IsFollowedByASaveInTheSameScope_NotAPhantomConflict()
    {
        var processId = await StartAsync();
        await RefuseEventsAsync(true);

        using var response = await PostStepAsync(PersistenceSteps.Record, processId);

        Assert.NotEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, _counter.Recorded);
        await RefuseEventsAsync(false);
        Assert.NotEqual("completed", StepStatus(await StateAsync(processId), PersistenceSteps.Record));
    }

    private async Task<Guid> StartAsync()
    {
        using var response = await PostStepAsync(PersistenceSteps.Start, processId: null);
        var body = Parse(await response.Content.ReadAsStringAsync(Cancel));
        Assert.True(response.StatusCode == HttpStatusCode.OK, body.ToString());
        return body.GetProperty("processId").GetGuid();
    }

    private async Task RefuseEventsAsync(bool refuse)
    {
        await using var scope = _host!.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database
            .ExecuteSqlRawAsync(refuse ? "UPDATE TestSwitch SET RefuseEvents = 1;" : "UPDATE TestSwitch SET RefuseEvents = 0;", Cancel);
    }

    private Task<HttpResponseMessage> PostStepAsync(string step, Guid? processId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/{ServiceName}/processes/steps/{step.ToLowerInvariant()}")
        {
            Content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json"),
        };

        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString());
        }

        return _client.SendAsync(request, Cancel);
    }

    private async Task<JsonElement> StateAsync(Guid processId)
    {
        using var response = await _client.GetAsync($"/{ServiceName}/processes/{processId}", Cancel);
        var body = await response.Content.ReadAsStringAsync(Cancel);
        Assert.True(response.IsSuccessStatusCode, $"State read failed ({(int)response.StatusCode}): {body}");
        return Parse(body);
    }

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

    private sealed class DiscardingPublisher : IEventPublisher
    {
        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent =>
            Task.CompletedTask;
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
