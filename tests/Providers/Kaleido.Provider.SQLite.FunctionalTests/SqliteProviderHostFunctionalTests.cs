using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Provider.SQLite.FunctionalTests;

public sealed class SqliteProviderHostFunctionalTests
{
    [Fact]
    public async Task FileBackedHost_AfterRestart_RetainsPinnedHandoffStateOpaqueReplayAndOutbox()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaleido-sqlite-restart-{Guid.NewGuid():N}.db");
        try
        {
            Guid processId;
            var admission = Admission("orders:3:handoff");
            using (var before = BuildHost(path))
            {
                await EnsureCreatedAsync(before);
                await before.StartAsync(TestContext.Current.CancellationToken);
                using var scope = before.Services.CreateScope();
                var runtime = scope.ServiceProvider.GetRequiredService<IProcessorRuntime>();
                var handOff = await runtime.ExecuteAsync(ProcessRequest.ForStep(
                    new Handoffs.BeginHandoff { OrderId = "A-1" }), TestContext.Current.CancellationToken);
                processId = handOff.ProcessId;
                Assert.Equal(("3", "catalog", "2"), (handOff.ServiceVersion, handOff.TargetProcessorName, handOff.TargetProcessorVersion));
                var store = scope.ServiceProvider.GetRequiredService<IProcessIdempotencyStore>();
                Assert.Null(await store.AdmitAsync(admission, TestContext.Current.CancellationToken));
                Assert.True(await store.CompleteAsync(admission with
                {
                    State = IdempotencyState.Completed,
                    Replay = new IdempotencyReplay("transport/binary-v1", [0, 255, 10]),
                }, TestContext.Current.CancellationToken));
                await before.StopAsync(TestContext.Current.CancellationToken);
            }

            using (var after = BuildHost(path))
            {
                await after.StartAsync(TestContext.Current.CancellationToken);
                using var scope = after.Services.CreateScope();
                var context = await scope.ServiceProvider.GetRequiredService<IProcessContextStore>()
                    .LoadAsync(new ProcessKey("orders", "3", processId), TestContext.Current.CancellationToken);
                var state = ProcessStateSnapshot.FromContext(Assert.IsType<ProcessContext>(context));
                Assert.Equal(("orders", "3", "catalog", "2"),
                    (state.ServiceName, state.ServiceVersion, state.TargetProcessorName, state.TargetProcessorVersion));
                var replay = await scope.ServiceProvider.GetRequiredService<IProcessIdempotencyStore>()
                    .AdmitAsync(admission with { AdmissionId = Guid.NewGuid() }, TestContext.Current.CancellationToken);
                Assert.Equal(admission.AdmissionId, Assert.IsType<IdempotencyRecord>(replay).AdmissionId);
                Assert.Equal("transport/binary-v1", Assert.IsType<IdempotencyReplay>(replay.Replay).Format);
                Assert.Equal([0, 255, 10], replay.Replay.Payload);
                var events = await scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>()
                    .ClaimEventsAsync(20, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
                Assert.Contains(events, x => x.EventType == ProcessEventTypes.ProcessStepCompleted);
                await after.StopAsync(TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task StateAndOutboxWrites_WhenAProviderSessionRollsBack_KeepNeither()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaleido-sqlite-rollback-{Guid.NewGuid():N}.db");
        try
        {
            using var host = BuildHost(path);
            await EnsureCreatedAsync(host);
            await host.StartAsync(TestContext.Current.CancellationToken);
            var processId = Guid.NewGuid();
            using (var writer = host.Services.CreateScope())
            {
                await using (await writer.ServiceProvider.GetRequiredService<IProcessTransactions>()
                    .BeginAsync(TestContext.Current.CancellationToken))
                {
                    await writer.ServiceProvider.GetRequiredService<IProcessContextStore>().SaveAsync(new ProcessContext
                    {
                        ProcessId = processId,
                        ProcessorName = "orders",
                        ProcessorVersion = "3",
                        State = ProcessExecutionState.Active,
                    }, TestContext.Current.CancellationToken);
                    await writer.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>().AddAsync(
                        [new KaleidoOutboxEvent
                        {
                            EventId = Guid.NewGuid(),
                            EventType = "test.rollback",
                            Payload = "{}",
                            ServiceName = "orders",
                            ServiceVersion = "3",
                            OccurredUtc = DateTimeOffset.UtcNow,
                        }], TestContext.Current.CancellationToken);
                }
            }

            using (var reader = host.Services.CreateScope())
            {
                Assert.Null(await reader.ServiceProvider.GetRequiredService<IProcessContextStore>()
                    .LoadAsync(new ProcessKey("orders", "3", processId), TestContext.Current.CancellationToken));
                Assert.Empty(await reader.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>()
                    .ClaimEventsAsync(20, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
            }

            await host.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    private static IdempotencyRecord Admission(string key)
    {
        var now = TimeProvider.System.GetUtcNow();
        return new IdempotencyRecord
        {
            Key = key,
            ProcessorName = "orders",
            ProcessorVersion = "3",
            AdmissionId = Guid.NewGuid(),
            RequestHash = "handoff-payload",
            State = IdempotencyState.InProgress,
            LeaseUntilUtc = now.AddMinutes(5),
            ExpiresUtc = now.AddHours(1),
        };
    }

    private static IHost BuildHost(string path) => Host.CreateDefaultBuilder()
        .UseEnvironment("Production")
        .UseDefaultServiceProvider((_, options) =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = true;
        })
        .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:orders"] = $"Data Source={path};Pooling=False",
        }))
        .ConfigureServices((context, services) => services.AddKaleido(context.Configuration, new()
        {
            ServiceName = "orders",
            ServiceVersion = "3",
            Assemblies = [typeof(Handoffs.BeginHandoff).Assembly],
            TypeFilter = type => type.Namespace == typeof(Handoffs.BeginHandoff).Namespace,
        }).AddHosting().AddSqliteProviders("orders"))
        .Build();

    private static async Task EnsureCreatedAsync(IHost host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>()
            .Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }
}
