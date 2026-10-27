using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Eventing;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Execution;

// End to end against the SQLite reference store (a real database file): events go to the outbox in
// the same database as the process state, and the relay of each host hands them to the publisher
// until it succeeds - across publisher failures, restarts and several hosts on one database.
public sealed class EventOutboxTests : IAsyncLifetime
{
    private const string RootStepUrl = "/kaleido/processes/steps/runtimerootstep";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-outbox-{Guid.NewGuid():N}.db");

    private readonly List<IHost> _hosts = [];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Stopping waits for each host's relay to finish, so nothing holds the database file any more.
    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }

        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task APublisherThatFailsTwice_GetsEveryEventExactlyOnceAfterRetries()
    {
        var publisher = new RecordingPublisher(failFirst: 2);
        var host = await StartHostAsync(publisher);

        using var response = await PostStepAsync(host.GetTestClient());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WaitUntilAsync(async () => await OutboxCountAsync() == 0);

        Assert.Equal(publisher.Published.Count, publisher.Published.Select(x => x.EventId).Distinct().Count());
        Assert.Equal(publisher.Published.Count + 2, publisher.Calls);
        Assert.Contains(publisher.Published, x => x.EventType == KaleidoEventTypes.ProcessCreated);
        Assert.Contains(publisher.Published, x => x.EventType == KaleidoEventTypes.ProcessPlanBuilt);
        Assert.Contains(publisher.Published, x => x.EventType == KaleidoEventTypes.ProcessStepCompleted);
        Assert.Contains(publisher.Published, x => x.EventType == KaleidoEventTypes.ProcessExecutionCompleted);
    }

    // #325: the whole path over the network - execute over HTTP, process state and events in SQLite,
    // the relay publishing each event over HTTP to a collector (like the PriorAuth EventCollector).
    // The collector refuses the first deliveries; every event still arrives, once, with its context.
    [Fact]
    public async Task EventsReachACollectorOverHttp_ExactlyOnce_AfterItRefusesTheFirstDeliveries()
    {
        var collector = await StartCollectorAsync(refuseFirst: 3);
        var publisher = new HttpPublisher(collector.Host.GetTestClient());
        var host = await StartHostAsync(publisher);
        var requestId = $"req-{Guid.NewGuid():N}";

        using var request = new HttpRequestMessage(HttpMethod.Post, RootStepUrl)
        {
            Content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add(KaleidoCorrelationHeaders.RequestId, requestId);
        using var response = await host.GetTestClient().SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var processId = JsonNode.Parse(await response.Content.ReadAsStringAsync(Cancel))!["processId"]!.GetValue<Guid>();

        await WaitUntilAsync(async () => await OutboxCountAsync() == 0);

        var received = collector.Received.ToArray();
        Assert.Equal(3, collector.Refused);
        Assert.Equal(received.Length, received.Select(x => x["event"]!["eventId"]!.GetValue<Guid>()).Distinct().Count());
        Assert.Equal(
            [KaleidoEventTypes.ProcessCreated, KaleidoEventTypes.ProcessExecutionCompleted, KaleidoEventTypes.ProcessPlanBuilt, KaleidoEventTypes.ProcessStepCompleted],
            received.Select(x => x["eventType"]!.GetValue<string>()).Distinct().Order(StringComparer.Ordinal));
        Assert.All(received, x =>
        {
            Assert.Equal(requestId, x["context"]!["requestId"]!.GetValue<string>());
            Assert.Equal(processId, x["context"]!["processId"]!.GetValue<Guid>());
        });
    }

    [Fact]
    public async Task EventsThatCouldNotBePublished_SurviveARestartAndArePublishedByTheNextHost()
    {
        var host = await StartHostAsync(new RecordingPublisher(alwaysFail: true), retryDelay: TimeSpan.FromMinutes(10));
        using var response = await PostStepAsync(host.GetTestClient());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var written = await OutboxCountAsync();

        // Every event was attempted (and failed) once; nothing is lost.
        await WaitUntilAsync(async () => await ScalarAsync("SELECT COUNT(*) FROM OutboxEvents WHERE Attempts = 0") is 0L);
        await host.StopAsync(Cancel);

        Assert.True(written > 0);
        Assert.Equal(written, await OutboxCountAsync());

        // The retry time has come (instead of waiting ten minutes).
        await ExecuteAsync("UPDATE OutboxEvents SET NextAttemptUtc = 0");

        var publisher = new RecordingPublisher();
        await StartHostAsync(publisher);

        await WaitUntilAsync(async () => await OutboxCountAsync() == 0);
        Assert.Equal(written, publisher.Published.Count);
    }

    [Fact]
    public async Task TwoHostsOnOneDatabase_PublishEveryEventExactlyOnce()
    {
        var first = new RecordingPublisher();
        var second = new RecordingPublisher();
        var firstHost = await StartHostAsync(first);
        var secondHost = await StartHostAsync(second);

        for (var i = 0; i < 5; i++)
        {
            using var a = await PostStepAsync(firstHost.GetTestClient());
            using var b = await PostStepAsync(secondHost.GetTestClient());
        }

        await WaitUntilAsync(async () => await OutboxCountAsync() == 0);

        var all = first.Published.Concat(second.Published).Select(x => x.EventId).ToList();
        Assert.Equal(all.Count, all.Distinct().Count());
        Assert.True(all.Count >= 20);
    }

    [Fact]
    public async Task AQuery_PublishesItsQueryExecutedEventThroughTheOutbox()
    {
        var publisher = new RecordingPublisher();
        var host = await StartHostAsync(publisher);

        using var content = new StringContent("""{ "query": {} }""", Encoding.UTF8, "application/json");
        using var response = await host.GetTestClient().PostAsync("/kaleido/queryable/functionalrecordcontextsource/query", content, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WaitUntilAsync(() => Task.FromResult(publisher.Published.Any(x => x.EventType == KaleidoEventTypes.QueryExecuted)));
        Assert.Equal(0L, await OutboxCountAsync());
    }

    // A value classified as sensitive is redacted before the outbox write: the outbox row and the
    // publisher see the redactor's output, the caller's response the real value.
    [Fact]
    public async Task AClassifiedValue_IsRedactedInTheOutboxAndForThePublisher_ButNotInTheResponse()
    {
        var failing = new RecordingPublisher(alwaysFail: true);
        var host = await StartHostAsync(failing, retryDelay: TimeSpan.FromMinutes(10), configureKaleido: AddMaskingRedaction);

        var names = await QueryNamesAsync(host);

        await WaitUntilAsync(async () => await ScalarAsync("SELECT COUNT(*) FROM OutboxEvents WHERE Attempts = 0") is 0L);
        var payload = (string)(await ScalarAsync($"SELECT Payload FROM OutboxEvents WHERE EventType = '{KaleidoEventTypes.QueryExecuted}'"))!;
        await host.StopAsync(Cancel);

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.DoesNotContain(name, payload, StringComparison.Ordinal));
        Assert.Equal(names.Count, RecordNames(JsonNode.Parse(payload)!["event"]!).Count(x => x == "***"));

        await ExecuteAsync("UPDATE OutboxEvents SET NextAttemptUtc = 0");
        var publisher = new RecordingPublisher();
        await StartHostAsync(publisher, configureKaleido: AddMaskingRedaction);

        await WaitUntilAsync(() => Task.FromResult(publisher.Events.Any(x => x.EventType == KaleidoEventTypes.QueryExecuted)));
        var published = publisher.Events.First(x => x.EventType == KaleidoEventTypes.QueryExecuted).Event;
        Assert.All(RecordNames(published), name => Assert.Equal("***", name));
    }

    [Fact]
    public async Task AClassifiedValue_WithoutARedactorRegistered_IsErased()
    {
        var publisher = new RecordingPublisher();
        var host = await StartHostAsync(publisher);

        var names = await QueryNamesAsync(host);

        await WaitUntilAsync(() => Task.FromResult(publisher.Events.Any(x => x.EventType == KaleidoEventTypes.QueryExecuted)));
        var published = publisher.Events.First(x => x.EventType == KaleidoEventTypes.QueryExecuted).Event;
        Assert.NotEmpty(names);
        Assert.All(RecordNames(published), name => Assert.Equal(string.Empty, name));
    }

    // The same classification on a step input: the step-completed event carries the request, and
    // the classified value must not reach the outbox or the publisher in the clear.
    [Fact]
    public async Task AClassifiedStepInput_IsRedactedInTheStepCompletedEvent()
    {
        const string remark = "patient said something private";
        var publisher = new RecordingPublisher();
        await StartHostAsync(publisher, configureKaleido: AddMaskingRedaction);
        var host = _hosts[^1];

        using var content = new StringContent(
            $$$"""{"processStep":{"itemId":"{{{Guid.NewGuid()}}}","remark":"{{{remark}}}"}}""",
            Encoding.UTF8,
            "application/json");
        using var response = await host.GetTestClient().PostAsync("/kaleido/processes/steps/runtimeinputstep", content, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WaitUntilAsync(() => Task.FromResult(publisher.Events.Any(x => x.EventType == KaleidoEventTypes.ProcessStepCompleted)));

        var published = publisher.Events.First(x => x.EventType == KaleidoEventTypes.ProcessStepCompleted).Event.ToJsonString();
        Assert.DoesNotContain(remark, published, StringComparison.Ordinal);
        Assert.Contains("***", published, StringComparison.Ordinal);
    }

    // The event policy (ADR 0025): the host's AddRedaction is for logs and does not reach events.
    private static void AddMaskingRedaction(IKaleidoBuilder kaleido) =>
        kaleido.AddEventRedaction(redaction => redaction.SetRedactor<MaskingRedactor>(new DataClassificationSet(FunctionalSensitiveDataAttribute.Sensitive)));

    // Runs the query and returns the names the caller got (the real values).
    private static async Task<List<string>> QueryNamesAsync(IHost host)
    {
        using var content = new StringContent("""{ "query": {} }""", Encoding.UTF8, "application/json");
        using var response = await host.GetTestClient().PostAsync("/kaleido/queryable/functionalrecordcontextsource/query", content, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync(Cancel))!;
        return [.. body["results"]!.AsArray().Select(x => x!["name"]!.GetValue<string>())];
    }

    private static List<string?> RecordNames(JsonNode queryExecuted) =>
        [.. queryExecuted["records"]!.AsArray().Select(x => x!["name"]?.GetValue<string>())];

    // Like the PriorAuth services: the host creates the store's schema at startup.
    private async Task<IHost> StartHostAsync(IEventPublisher publisher, TimeSpan? retryDelay = null, Action<IKaleidoBuilder>? configureKaleido = null)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddSingleton<FunctionalRecordData>();
                    var kaleido = services.AddKaleido(new ConfigurationBuilder().Build(), new()
                    {
                        ServiceName = FunctionalProcessorNames.TestProcessor,
                        AuthorizationMode = KaleidoAuthorizationMode.None,
                        Assemblies = [typeof(RuntimeRootStep).Assembly],
                        Events = new() { Resilience = new() { RetryDelay = retryDelay ?? TimeSpan.FromMilliseconds(50) } },
                    })
                        .AddInMemoryStores()
                        .AddHttp()
                        .AddSqliteContextStore($"Data Source={_databasePath}").AddSqliteOutboxStore($"Data Source={_databasePath}").AddSqliteIdempotencyStore($"Data Source={_databasePath}");
                    services.RemoveAll<IEventPublisher>();
                    services.AddSingleton<IEventPublisher>(publisher);
                    configureKaleido?.Invoke(kaleido);
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(Cancel);

        _hosts.Add(host);

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);

        return host;
    }

    private static async Task<HttpResponseMessage> PostStepAsync(HttpClient client)
    {
        using var content = new StringContent("""{"processStep":{}}""", Encoding.UTF8, "application/json");
        return await client.PostAsync(RootStepUrl, content, Cancel);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (!await condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The outbox did not drain in time.");
            await Task.Delay(50, Cancel);
        }
    }

    private async Task<long> OutboxCountAsync() =>
        (long)(await ScalarAsync("SELECT COUNT(*) FROM OutboxEvents"))!;

    private async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(Cancel);
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(Cancel);
    }

    // Records what it was handed; fails the first calls (or every call) on request.
    private sealed class RecordingPublisher(int failFirst = 0, bool alwaysFail = false) : IEventPublisher
    {
        private int _calls;

        public ConcurrentQueue<(Guid EventId, string EventType)> Published { get; } = new();

        public ConcurrentQueue<(string EventType, JsonNode Event)> Events { get; } = new();

        public int Calls => Volatile.Read(ref _calls);

        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent
        {
            var call = Interlocked.Increment(ref _calls);

            if (alwaysFail || call <= failFirst)
            {
                return Task.FromException(new InvalidOperationException("broker down"));
            }

            Published.Enqueue((envelope.Event.EventId, envelope.EventType));
            Events.Enqueue((envelope.EventType, JsonSerializer.SerializeToNode(envelope.Event, JsonOptions)!));
            return Task.CompletedTask;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Publishes each event as its JSON envelope over HTTP; a non-success answer is a failed attempt.
    private sealed class HttpPublisher(HttpClient collector) : IEventPublisher
    {
        public async Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent
        {
            using var response = await collector.PostAsJsonAsync("/events", envelope, JsonOptions, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    // A collector that answers 503 to its first deliveries, then records every envelope it receives.
    private sealed class Collector(int refuseFirst)
    {
        private int _deliveries;

        public IHost Host { get; set; } = null!;

        public ConcurrentQueue<JsonNode> Received { get; } = new();

        public int Refused => Math.Min(Volatile.Read(ref _deliveries), refuseFirst);

        public async Task<IResult> ReceiveAsync(HttpRequest request)
        {
            if (Interlocked.Increment(ref _deliveries) <= refuseFirst)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            Received.Enqueue((await JsonNode.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted))!);
            return Results.Ok();
        }
    }

    private async Task<Collector> StartCollectorAsync(int refuseFirst)
    {
        var collector = new Collector(refuseFirst);

        collector.Host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services => services.AddRouting());
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapPost("/events", (HttpRequest request) => collector.ReceiveAsync(request)));
                });
            })
            .StartAsync(Cancel);

        _hosts.Add(collector.Host);
        return collector;
    }

    // Replaces every classified value with ***.
    private sealed class MaskingRedactor : Redactor
    {
        public override int GetRedactedLength(ReadOnlySpan<char> input) => 3;

        public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
        {
            "***".AsSpan().CopyTo(destination);
            return 3;
        }
    }
}
