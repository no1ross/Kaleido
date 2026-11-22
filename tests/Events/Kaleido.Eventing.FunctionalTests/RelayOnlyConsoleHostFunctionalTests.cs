using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kaleido.Http;
using Kaleido.Http.Server;
using Kaleido.Process;
using Kaleido.Process.Eventing;
using Kaleido.Process.Execution;
using Kaleido.Process.Http.Server;
using Kaleido.Provider.SQLite;
using Kaleido.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Eventing.FunctionalTests.Relay;

public sealed class RelayOnlyConsoleHostFunctionalTests : IAsyncLifetime
{
    private const int StepsPerService = 3;
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-relay-{Guid.NewGuid():N}.db");
    private readonly List<IHost> hosts = [];
    private string ConnectionString => $"Data Source={databasePath};Pooling=False";

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var host in hosts.AsEnumerable().Reverse())
        {
            await host.StopAsync(CancellationToken.None);
            host.Dispose();
        }

        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }

    [Fact]
    public async Task AConsoleRelayHost_PublishesBothWritersEventsOnce()
    {
        var orders = await StartWebWriterAsync("orders");
        var returns = await StartWriterAsync("returns");
        await ExecuteWebStepsAsync(orders, "orders");
        await ExecuteStepsAsync(returns);
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");
        Assert.True(written > 0);
        var published = new Published();

        await StartRelayAsync(published);
        await published.WaitForAttemptsAsync((int)written).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxEvents", expected: 0);

        Assert.Equal(written, published.Events.Count);
        Assert.Equal(written, published.Events.Select(item => item.EventId).Distinct().Count());
        Assert.Equal(["orders", "returns"], published.Events.Select(item => item.ServiceName).Distinct().Order(StringComparer.Ordinal));
        var health = await orders.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Name == "kaleido-outbox", Cancel);
        Assert.Equal(HealthStatus.Healthy, health.Status);
    }

    [Fact]
    public async Task TwoConsoleRelayHosts_SharingOneDatabase_NeverPublishTheSameEventTwice()
    {
        var orders = await StartWriterAsync("orders");
        var returns = await StartWriterAsync("returns");
        await ExecuteStepsAsync(orders);
        await ExecuteStepsAsync(returns);
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");
        var published = new Published(holdFirst: true);

        await StartRelayAsync(published, batchSize: 3);
        await published.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await StartRelayAsync(published, batchSize: 3);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxRelays", expected: 2);
        published.ReleaseFirst();
        await published.WaitForAttemptsAsync((int)written).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxEvents", expected: 0);

        Assert.Equal(written, published.Events.Count);
        Assert.Equal(written, published.Events.Select(item => item.EventId).Distinct().Count());
    }

    [Fact]
    public async Task AConsoleRelayHost_AfterTwoPublisherFailures_DeliversEachEventOnce()
    {
        var orders = await StartWriterAsync("orders");
        await ExecuteStepsAsync(orders);
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow.AddMinutes(1));
        var published = new Published(failFirst: 2);

        await StartRelayAsync(published, time: time);
        await published.WaitForAttemptsAsync(2).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        for (var tick = 0; tick < 20 && published.Events.Count < written; tick++)
        {
            await Task.Yield();
            time.Advance(TimeSpan.FromSeconds(2));
        }

        await published.WaitForAttemptsAsync((int)written + 2).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxEvents", expected: 0);
        Assert.Equal(written + 2, published.Attempts);
        Assert.Equal(written, published.Events.Count);
        Assert.Equal(written, published.Events.Select(item => item.EventId).Distinct().Count());
    }

    [Fact]
    public async Task AnHttpCollector_AfterThreeRefusals_ReceivesEachProcessEventWithItsCorrelation()
    {
        var orders = await StartWebWriterAsync("orders");
        var requestId = $"request-{Guid.NewGuid():N}";
        using var client = orders.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/orders/1/processes/steps/startstep")
        {
            Content = JsonContent.Create(new { processStep = new { } }, options: KaleidoJsonOptions.Options),
        };
        request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.RequestId, requestId);
        using var response = await client.SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel);
        var processId = Assert.IsType<ProcessResult>(result).ProcessId;
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");
        var collector = new Collector(expected: (int)written, refuseFirst: 3);
        var collectorHost = await StartCollectorAsync(collector);
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow.AddMinutes(1));

        await StartCollectorRelayAsync(collectorHost, time);
        await collector.FirstRefusals.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxEvents", expected: 3);
        for (var tick = 0; tick < 20 && !collector.Completed.IsCompleted; tick++)
        {
            await Task.Yield();
            time.Advance(TimeSpan.FromSeconds(2));
        }

        await collector.Completed.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxEvents", expected: 0);
        Assert.Equal(3, collector.Refused);
        var received = collector.Received.ToArray();
        Assert.Equal(written, received.Length);
        Assert.Equal(written, received.Select(item => item.GetProperty("event").GetProperty("eventId").GetGuid()).Distinct().Count());
        Assert.Equal([ProcessEventTypes.ProcessCreated, ProcessEventTypes.ProcessExecutionCompleted,
            ProcessEventTypes.ProcessPlanBuilt, ProcessEventTypes.ProcessStepCompleted],
            received.Select(item => item.GetProperty("eventType").GetString()).Order(StringComparer.Ordinal));
        Assert.All(received, item =>
        {
            Assert.Equal(requestId, item.GetProperty("context").GetProperty("requestId").GetString());
            Assert.Equal(processId, item.GetProperty("context").GetProperty("processId").GetGuid());
        });
    }

    [Fact]
    public async Task AConsoleRelayHost_AfterItsPublisherFailsAndTheHostRestarts_DrainsTheSameRows()
    {
        var orders = await StartWriterAsync("orders");
        await ExecuteStepsAsync(orders);
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow.AddMinutes(1));
        var failing = new Published(alwaysFail: true);
        var first = await StartRelayAsync(failing, time: time);
        await failing.WaitForAttemptsAsync((int)written).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxEvents WHERE Attempts = 1", written);
        await first.StopAsync(Cancel);
        first.Dispose();
        hosts.Remove(first);
        time.Advance(TimeSpan.FromSeconds(3));

        var recovered = new Published();
        await StartRelayAsync(recovered, time: time);
        await recovered.WaitForAttemptsAsync((int)written).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxEvents", expected: 0);

        Assert.Empty(failing.Events);
        Assert.Equal(written, recovered.Events.Count);
        Assert.Equal(written, recovered.Events.Select(item => item.EventId).Distinct().Count());
    }

    [Fact]
    public async Task AConsoleRelayHost_WhenPublishingKeepsFailing_RetriesThenParksEveryEvent()
    {
        var orders = await StartWriterAsync("orders");
        await ExecuteStepsAsync(orders);
        var written = await CountAsync("SELECT COUNT(*) FROM OutboxEvents");
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow.AddMinutes(1));
        var published = new Published(alwaysFail: true);

        await StartRelayAsync(published, maxRetryAttempts: 1, time: time);
        await published.WaitForAttemptsAsync((int)written).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        for (var tick = 0; tick < 10 && published.Attempts < written * 2; tick++)
        {
            await Task.Yield();
            time.Advance(TimeSpan.FromSeconds(2));
        }

        await published.WaitForAttemptsAsync((int)(written * 2)).WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        await WaitForCountAsync("SELECT COUNT(*) FROM OutboxEvents WHERE IsParked = 1", written);
        Assert.Equal(written * 2, published.Attempts);
        Assert.Equal(written, await CountAsync("SELECT COUNT(*) FROM OutboxEvents WHERE Attempts = 2"));
        Assert.Empty(published.Events);
    }

    private static IHostBuilder Builder() => Host.CreateDefaultBuilder()
        .UseEnvironment("Production")
        .UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateOnBuild = true;
            options.ValidateScopes = true;
        });

    private async Task<IHost> StartWriterAsync(string serviceName)
    {
        var host = Builder().ConfigureServices(services => AddWriterServices(services, serviceName, http: false)).Build();
        await EnsureSchemaAsync(host);
        await host.StartAsync(Cancel);
        hosts.Add(host);
        return host;
    }

    private async Task<WebApplication> StartWebWriterAsync(string serviceName)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        AddWriterServices(builder.Services, serviceName, http: true);
        var app = builder.Build();
        app.MapProcessor();
        await EnsureSchemaAsync(app);
        await app.StartAsync(Cancel);
        hosts.Add(app);
        return app;
    }

    private void AddWriterServices(IServiceCollection services, string serviceName, bool http)
    {
        var kaleido = services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = serviceName,
            ServiceVersion = "1",
            Assemblies = [typeof(RelayOnlyConsoleHostFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(StartStep) || type == typeof(StartHandler),
        })
            .AddEventing()
            .AddProcess()
            .AddSqliteContextProvider(ConnectionString)
            .AddSqliteOutboxProvider(ConnectionString);
        if (http)
        {
            kaleido.AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
                .AddHttp()
                .AddProcessHttpServer()
                .AddSqliteIdempotencyProvider(ConnectionString);
        }
    }

    private static async Task EnsureSchemaAsync(IHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
    }

    private async Task<IHost> StartRelayAsync(Published published, int batchSize = 20, int maxRetryAttempts = 9, TimeProvider? time = null)
    {
        var host = Builder().ConfigureServices(services =>
        {
            if (time is not null)
            {
                services.AddSingleton(time);
            }

            services.AddSingleton(new ConsolePublisher(published));
            services.AddKaleido(new ConfigurationBuilder().Build(), new()
            {
                ServiceName = "outbox-relay",
                ServiceVersion = "1",
                Assemblies = [typeof(RelayOnlyConsoleHostFunctionalTests).Assembly],
                TypeFilter = _ => false,
            })
                .AddEventing(new KaleidoEventOptions
                {
                    BatchSize = batchSize,
                    Resilience = new() { MaxRetryAttempts = maxRetryAttempts, RetryDelay = TimeSpan.FromSeconds(1), CircuitBreakerMinimumThroughput = 1000 },
                })
                .AddProcessEventDispatch()
                .AddQueryableEventDispatch()
                .AddSqliteOutboxProvider(ConnectionString)
                .AddOutboxRelay<ConsolePublisher>();
        }).Build();
        await host.StartAsync(Cancel);
        hosts.Add(host);
        return host;
    }

    private async Task<WebApplication> StartCollectorAsync(Collector collector)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapPost("/collector/events", collector.ReceiveAsync);
        await app.StartAsync(Cancel);
        hosts.Add(app);
        return app;
    }

    private async Task StartCollectorRelayAsync(WebApplication collector, FakeTimeProvider time)
    {
        var host = Builder().ConfigureServices(services =>
        {
            services.AddSingleton<TimeProvider>(time);
            services.AddSingleton(new HttpCollectorPublisher(collector.GetTestClient()));
            services.AddKaleido(new ConfigurationBuilder().Build(), new()
            {
                ServiceName = "outbox-relay",
                ServiceVersion = "1",
                Assemblies = [typeof(RelayOnlyConsoleHostFunctionalTests).Assembly],
                TypeFilter = _ => false,
            })
                .AddEventing(new KaleidoEventOptions
                {
                    Resilience = new() { MaxRetryAttempts = 4, RetryDelay = TimeSpan.FromSeconds(1), CircuitBreakerMinimumThroughput = 1000 },
                })
                .AddProcessEventDispatch()
                .AddQueryableEventDispatch()
                .AddSqliteOutboxProvider(ConnectionString)
                .AddOutboxRelay<HttpCollectorPublisher>();
        }).Build();
        await host.StartAsync(Cancel);
        hosts.Add(host);
    }

    private static async Task ExecuteWebStepsAsync(WebApplication writer, string serviceName)
    {
        using var client = writer.GetTestClient();
        for (var step = 0; step < StepsPerService; step++)
        {
            using var response = await client.PostAsJsonAsync($"/{serviceName}/1/processes/steps/startstep",
                new { processStep = new { } }, KaleidoJsonOptions.Options, Cancel);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static async Task ExecuteStepsAsync(IHost writer)
    {
        for (var step = 0; step < StepsPerService; step++)
        {
            await using var scope = writer.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<IProcessorRuntime>()
                .ExecuteAsync(ProcessRequest.ForStep(new StartStep()), Cancel);
            Assert.Equal(StepExecutionStatus.Completed, Assert.Single(result.Steps).ExecutionStatus);
        }
    }

    private async Task WaitForCountAsync(string query, long expected)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (await CountAsync(query) != expected)
        {
            timeout.Token.ThrowIfCancellationRequested();
            await Task.Yield();
        }
    }

    private async Task<long> CountAsync(string query)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = query;
        return (long)(await command.ExecuteScalarAsync(Cancel) ?? 0L);
    }

    private sealed class Collector(int expected, int refuseFirst)
    {
        private int refused;
        private int calls;
        private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource firstRefusals = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<JsonElement> Received { get; } = new();
        public Task Completed => completed.Task;
        public Task FirstRefusals => firstRefusals.Task;
        public int Refused => Volatile.Read(ref refused);

        public async Task<IResult> ReceiveAsync(HttpRequest request)
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            if (Interlocked.Increment(ref calls) <= refuseFirst)
            {
                if (Interlocked.Increment(ref refused) == refuseFirst)
                {
                    firstRefusals.TrySetResult();
                }

                return Results.StatusCode(503);
            }

            Received.Enqueue(document.RootElement.Clone());
            if (Received.Count >= expected)
            {
                completed.TrySetResult();
            }

            return Results.Ok();
        }
    }

    private sealed class HttpCollectorPublisher(HttpClient client) : IEventPublisher
    {
        public async Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
            where TEvent : IKaleidoEvent
        {
            using var response = await client.PostAsJsonAsync("/collector/events", envelope, KaleidoJsonOptions.Options, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }

    private sealed class Published(bool holdFirst = false, bool alwaysFail = false, int failFirst = 0)
    {
        private int attempts;
        private readonly ConcurrentDictionary<int, TaskCompletionSource> reached = new();
        private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ConcurrentQueue<(Guid EventId, string ServiceName)> Events { get; } = new();
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Attempts => Volatile.Read(ref attempts);
        public void ReleaseFirst() => released.TrySetResult();

        public Task WaitForAttemptsAsync(int count)
        {
            if (Attempts >= count)
            {
                return Task.CompletedTask;
            }

            var waiter = reached.GetOrAdd(count, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            if (Attempts >= count)
            {
                waiter.TrySetResult();
            }

            return waiter.Task;
        }

        public async Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
            where TEvent : IKaleidoEvent
        {
            var count = Interlocked.Increment(ref attempts);
            foreach (var (target, waiter) in reached)
            {
                if (count >= target)
                {
                    waiter.TrySetResult();
                }
            }

            if (holdFirst && count == 1)
            {
                FirstStarted.TrySetResult();
                await released.Task.WaitAsync(cancellationToken);
            }

            if (alwaysFail || count <= failFirst)
            {
                throw new InvalidOperationException("broker down");
            }

            Events.Enqueue((envelope.Event.EventId, envelope.Context switch
            {
                ProcessEventContext process => process.ServiceName,
                QueryableEventContext query => query.ServiceName,
                _ => string.Empty,
            }));
        }
    }

    private sealed class ConsolePublisher(Published published) : IEventPublisher
    {
        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
            where TEvent : IKaleidoEvent => published.PublishAsync(envelope, cancellationToken);
    }

    [ProcessStep(DisplayName = "Start", Description = "Starts a new relay test process")]
    public sealed record StartStep : IProcessStep;

    public sealed class StartHandler : IProcessStepHandler<StartStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(StartStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }
}
