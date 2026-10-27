using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Kaleido.Provider.SQLite;
using Kaleido.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests.Transitions;

// A process-state transition end to end on SQLite (ADR 0023): the context store and the outbox are
// separate implementations, and they are atomic because both write inside the transaction the SQLite
// provider opens on the request's database - Kaleido's own outbox store, or the host's own outbox
// joining that transaction.
public sealed class SqliteTransitionTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-transition-{Guid.NewGuid():N}.sqlite");

    private string ConnectionString => $"Data Source={_databasePath}";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    private async Task<ServiceProvider> BuildAsync(Action<IKaleidoBuilder> outbox)
    {
        var services = new ServiceCollection();
        services.AddDbContext<HostOutboxDbContext>(o => o.UseSqlite(ConnectionString));
        var builder = services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "transitions",
            AuthorizationMode = KaleidoAuthorizationMode.None,
            Assemblies = [typeof(SqliteTransitionTests).Assembly],
            TypeFilter = type => type.Namespace == typeof(SqliteTransitionTests).Namespace,
        })
            .AddSqliteContextStore(ConnectionString);
        outbox(builder);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
        await scope.ServiceProvider.GetRequiredService<HostOutboxDbContext>().Database.ExecuteSqlRawAsync(
            "CREATE TABLE HostOutboxMessages (EventId TEXT NOT NULL PRIMARY KEY, EventType TEXT NOT NULL, Payload TEXT NOT NULL)", Cancel);

        return provider;
    }

    // One request: Kaleido's transition, as the runtime runs it.
    private static async Task<ProcessorContext> TransitionAsync(ServiceProvider provider, ProcessorContext context, KaleidoOutboxEvent transitionEvent)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IProcessorTransition>().SaveAsync(context, transitionEvent, Cancel);
    }

    private static async Task<long> CountAsync(ServiceProvider provider, string table)
    {
        await using var scope = provider.CreateAsyncScope();
        var connection = scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)(await command.ExecuteScalarAsync(Cancel))!;
    }

    private static ProcessorContext NewProcess() => new() { ProcessId = Guid.NewGuid(), ProcessorName = "transitions" };

    private static KaleidoOutboxEvent Event() =>
        new() { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessStepCompleted, Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow };

    [Fact]
    public async Task KaleidosOutboxStore_StoresTheStateAndItsEventTogether()
    {
        await using var provider = await BuildAsync(kaleido => kaleido.AddSqliteOutboxStore(ConnectionString));

        await TransitionAsync(provider, NewProcess(), Event());

        Assert.Equal(1, await CountAsync(provider, "ProcessContexts"));
        Assert.Equal(1, await CountAsync(provider, "OutboxEvents"));
    }

    [Fact]
    public async Task KaleidosOutboxStore_AConflictingSave_StoresNoEvent()
    {
        await using var provider = await BuildAsync(kaleido => kaleido.AddSqliteOutboxStore(ConnectionString));
        var process = NewProcess();
        await TransitionAsync(provider, process, Event());

        await Assert.ThrowsAsync<KaleidoConcurrencyException>(() => TransitionAsync(provider, process, Event()));

        Assert.Equal(1, await CountAsync(provider, "OutboxEvents"));
    }

    // The event cannot be stored: the state is not kept either.
    [Fact]
    public async Task KaleidosOutboxStore_WhenTheEventCannotBeStored_TheStateIsNotKept()
    {
        await using var provider = await BuildAsync(kaleido => kaleido.AddSqliteOutboxStore(ConnectionString));
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.ExecuteSqlRawAsync("DROP TABLE OutboxEvents", Cancel);
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => TransitionAsync(provider, NewProcess(), Event()));

        Assert.Equal(0, await CountAsync(provider, "ProcessContexts"));
    }

    [Fact]
    public async Task TheHostsOwnOutbox_JoiningTheSqliteTransaction_StoresItsRowWithTheState()
    {
        await using var provider = await BuildAsync(kaleido => kaleido.AddOutbox<HostOutbox>());

        await TransitionAsync(provider, NewProcess(), Event());

        Assert.Equal(1, await CountAsync(provider, "ProcessContexts"));
        Assert.Equal(1, await CountAsync(provider, "HostOutboxMessages"));
        Assert.Equal(0, await CountAsync(provider, "OutboxEvents"));
    }

    [Fact]
    public async Task TheHostsOwnOutbox_AConflictingSave_StoresNoRow()
    {
        await using var provider = await BuildAsync(kaleido => kaleido.AddOutbox<HostOutbox>());
        var process = NewProcess();
        await TransitionAsync(provider, process, Event());

        await Assert.ThrowsAsync<KaleidoConcurrencyException>(() => TransitionAsync(provider, process, Event()));

        Assert.Equal(1, await CountAsync(provider, "HostOutboxMessages"));
    }

    // The host's own outbox table, in its own DbContext on the same database file.
    private sealed class HostOutboxMessage
    {
        public Guid EventId { get; set; }

        public string EventType { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }

    private sealed class HostOutboxDbContext(DbContextOptions<HostOutboxDbContext> options) : DbContext(options)
    {
        public DbSet<HostOutboxMessage> HostOutboxMessages => Set<HostOutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<HostOutboxMessage>().HasKey(x => x.EventId);
    }

    // As docs/ISqliteKaleidoTransaction describe: inside a transition, join the provider's transaction.
    private sealed class HostOutbox(HostOutboxDbContext db, ISqliteKaleidoTransaction transaction) : IKaleidoOutbox
    {
        public async Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default)
        {
            if (transaction.Current is { } current)
            {
                db.Database.SetDbConnection(current.Connection);
                await db.Database.UseTransactionAsync(current, cancellationToken);
            }

            db.HostOutboxMessages.AddRange(events.Select(x => new HostOutboxMessage { EventId = x.EventId, EventType = x.EventType, Payload = x.Payload }));
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
