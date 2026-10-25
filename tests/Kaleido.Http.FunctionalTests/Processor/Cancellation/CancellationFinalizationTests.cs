using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Kaleido.Eventing;
using Kaleido.Processor.Eventing;
using Kaleido.Provider.SQLite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.Cancellation;

// When the request is cancelled decides what is saved, against both stores (#286): cancelled before
// the handler, nothing runs; cancelled while it runs, the step is Canceled; cancelled after it
// returned, its completion and its step-completed event are saved, so a retry does not repeat it.
public sealed class CancellationFinalizationTests : IAsyncLifetime
{
    private const string ServiceName = "cancellation";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-cancellation-{Guid.NewGuid():N}.db");
    private readonly RequestCancellation _cancellation = new();
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

        _cancellation.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task CancelledBeforeTheHandler_DoesNotInvokeIt(string store)
    {
        await StartHostAsync(store);
        var processId = await StartProcessAsync();

        _cancellation.CancelBeforeHandling = true;
        using (await PostStepAsync(CancellationSteps.After, processId))
        {
        }

        _cancellation.CancelBeforeHandling = false;

        Assert.Equal(0, _cancellation.Invocations);
        Assert.NotEqual("completed", StepStatus(await StateAsync(processId), CancellationSteps.After));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task CancelledWhileTheHandlerRuns_SavesTheStepAsCanceled(string store)
    {
        await StartHostAsync(store);
        var processId = await StartProcessAsync();

        using (await PostStepAsync(CancellationSteps.During, processId))
        {
        }

        Assert.Equal(1, _cancellation.Invocations);
        Assert.Equal("canceled", StepStatus(await StateAsync(processId), CancellationSteps.During));
    }

    [Theory]
    [MemberData(nameof(Stores))]
    public async Task CancelledAfterTheHandlerReturned_SavesTheCompletionAndItsEvent(string store)
    {
        await StartHostAsync(store);
        var processId = await StartProcessAsync();

        using (await PostStepAsync(CancellationSteps.After, processId))
        {
        }

        Assert.Equal(1, _cancellation.Invocations);
        Assert.Equal("completed", StepStatus(await StateAsync(processId), CancellationSteps.After));
        await WaitUntilAsync(() => _publisher.StepsCompleted.Contains((processId, CancellationSteps.After)));
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
                    services.AddSingleton(_cancellation);
                    var kaleido = services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                        {
                            o.ServiceName = ServiceName;
                            o.AuthorizationMode = KaleidoAuthorizationMode.None;
                            o.Assemblies = [typeof(CancellationFinalizationTests).Assembly];
                            o.TypeFilter = type => type.Namespace == typeof(CancellationFinalizationTests).Namespace;
                            o.Events.Resilience.RetryDelay = TimeSpan.FromMilliseconds(50);
                        })
                        .AddInMemoryStores()
                        .AddHttp(o => o.IncludeFrameworkMessages = true);

                    if (store == "sqlite")
                    {
                        kaleido.AddSqliteContextStore($"Data Source={_databasePath}").AddSqliteOutboxStore($"Data Source={_databasePath}").AddSqliteIdempotencyStore($"Data Source={_databasePath}");
                    }

                    services.RemoveAll<IEventPublisher>();
                    services.AddSingleton<IEventPublisher>(_publisher);
                });
                web.Configure(app =>
                {
                    app.Use((context, next) =>
                    {
                        context.RequestAborted = _cancellation.Begin();
                        return next(context);
                    });
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

    private async Task<Guid> StartProcessAsync()
    {
        using var response = await PostStepAsync(CancellationSteps.Start, processId: null);
        var body = Parse(await response.Content.ReadAsStringAsync(Cancel));
        return body.GetProperty("processId").GetGuid();
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

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (!condition())
        {
            Assert.True(DateTimeOffset.UtcNow < deadline, "The step-completed event was not published in time.");
            await Task.Delay(50, Cancel);
        }
    }

    // Records the step-completed events the relay hands over.
    private sealed class RecordingPublisher : IEventPublisher
    {
        public ConcurrentBag<(Guid ProcessId, string StepName)> StepsCompleted { get; } = [];

        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent
        {
            if (envelope.Event is StepCompleted completed && envelope.Context is ProcessEventContext context)
            {
                StepsCompleted.Add((context.ProcessId, completed.StepName));
            }

            return Task.CompletedTask;
        }
    }
}
