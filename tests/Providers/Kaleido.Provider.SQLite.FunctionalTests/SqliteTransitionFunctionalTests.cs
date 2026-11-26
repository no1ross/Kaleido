using Kaleido.Process.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Provider.SQLite.FunctionalTests;

public sealed class SqliteTransitionFunctionalTests : IDisposable
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-transition-{Guid.NewGuid():N}.sqlite");
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;
    private string ConnectionString => $"Data Source={databasePath};Pooling=False";

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }

    [Fact]
    public async Task KaleidosOutboxStore_StoresTheStateAndItsEventTogether()
    {
        await using var provider = await BuildAsync(hostOutbox: false);

        await TransitionAsync(provider, NewProcess(), Guid.NewGuid());

        Assert.Equal(1, await CountAsync(provider, "ProcessContexts"));
        Assert.Equal(1, await CountAsync(provider, "OutboxEvents"));
    }

    [Fact]
    public async Task KaleidosOutboxStore_AConflictingSave_StoresNoEvent()
    {
        await using var provider = await BuildAsync(hostOutbox: false);
        var process = NewProcess();
        await TransitionAsync(provider, process, Guid.NewGuid());

        await Assert.ThrowsAsync<ProcessConcurrencyException>(() => TransitionAsync(provider, process, Guid.NewGuid()));

        Assert.Equal(1, await CountAsync(provider, "OutboxEvents"));
    }

    [Fact]
    public async Task KaleidosOutboxStore_WhenTheEventCannotBeStored_RollsBackTheNewState()
    {
        await using var provider = await BuildAsync(hostOutbox: false);
        var existingEvent = Guid.NewGuid();
        await TransitionAsync(provider, NewProcess(), existingEvent);

        await Assert.ThrowsAsync<DbUpdateException>(() => TransitionAsync(provider, NewProcess(), existingEvent));

        Assert.Equal(1, await CountAsync(provider, "ProcessContexts"));
        Assert.Equal(1, await CountAsync(provider, "OutboxEvents"));
    }

    [Fact]
    public async Task AHostsOwnOutboxStore_JoiningTheSqliteTransaction_StoresItsRowWithTheState()
    {
        await using var provider = await BuildAsync(hostOutbox: true);

        await TransitionAsync(provider, NewProcess(), Guid.NewGuid());

        Assert.Equal(1, await CountAsync(provider, "ProcessContexts"));
        Assert.Equal(1, await CountAsync(provider, "HostOutboxMessages"));
        Assert.Equal(0, await CountAsync(provider, "OutboxEvents"));
    }

    [Fact]
    public async Task AHostsOwnOutboxStore_AConflictingSave_StoresNoRow()
    {
        await using var provider = await BuildAsync(hostOutbox: true);
        var process = NewProcess();
        await TransitionAsync(provider, process, Guid.NewGuid());

        await Assert.ThrowsAsync<ProcessConcurrencyException>(() => TransitionAsync(provider, process, Guid.NewGuid()));

        Assert.Equal(1, await CountAsync(provider, "HostOutboxMessages"));
    }

    private async Task<ServiceProvider> BuildAsync(bool hostOutbox)
    {
        var services = new ServiceCollection();
        if (hostOutbox)
        {
            services.AddDbContext<HostOutboxDbContext>(options => options.UseSqlite(ConnectionString));
        }

        var builder = services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "orders",
            ServiceVersion = "3",
            Assemblies = [typeof(SqliteTransitionFunctionalTests).Assembly],
            TypeFilter = type => type.Namespace == typeof(Handoffs.BeginHandoff).Namespace,
        })
            .AddEventing()
            .AddProcess()
            .AddSqliteContextProvider(ConnectionString);
        if (hostOutbox)
        {
            builder.AddOutboxStore<HostOutboxStore>();
        }
        else
        {
            builder.AddSqliteOutboxProvider(ConnectionString);
        }

        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
        if (hostOutbox)
        {
            await scope.ServiceProvider.GetRequiredService<HostOutboxDbContext>().Database.ExecuteSqlRawAsync(
                "CREATE TABLE HostOutboxMessages (EventId TEXT NOT NULL PRIMARY KEY, EventType TEXT NOT NULL, Payload TEXT NOT NULL)", Cancel);
        }

        return provider;
    }

    private static ProcessContext NewProcess() => new()
    {
        ProcessId = Guid.NewGuid(),
        ProcessorName = "orders",
        ProcessorVersion = "3",
        State = ProcessExecutionState.Active,
    };

    private static async Task TransitionAsync(ServiceProvider provider, ProcessContext context, Guid eventId)
    {
        await using var scope = provider.CreateAsyncScope();
        await using var transaction = await scope.ServiceProvider.GetRequiredService<IProcessTransactions>().BeginAsync(Cancel);
        await scope.ServiceProvider.GetRequiredService<IProcessContextStore>().SaveAsync(context, Cancel);
        await scope.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>().AddAsync(
            [new KaleidoOutboxEvent
            {
                EventId = eventId,
                EventType = ProcessEventTypes.ProcessStepCompleted,
                ServiceName = "orders",
                ServiceVersion = "3",
                Payload = "{}",
                OccurredUtc = DateTimeOffset.UtcNow,
            }], Cancel);
        await transaction.CommitAsync(Cancel);
    }

    private static async Task<long> CountAsync(ServiceProvider provider, string table)
    {
        await using var scope = provider.CreateAsyncScope();
        var connection = scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.GetDbConnection();
        await connection.OpenAsync(Cancel);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return (long)(await command.ExecuteScalarAsync(Cancel) ?? 0L);
    }

    private sealed class HostOutboxMessage
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }

    private sealed class HostOutboxDbContext(DbContextOptions<HostOutboxDbContext> options) : DbContext(options)
    {
        public DbSet<HostOutboxMessage> HostOutboxMessages => Set<HostOutboxMessage>();
        protected override void OnModelCreating(ModelBuilder builder) => builder.Entity<HostOutboxMessage>().HasKey(message => message.EventId);
    }

    private sealed class HostOutboxStore(HostOutboxDbContext db, ISqliteKaleidoTransaction transaction) : IKaleidoOutboxStore
    {
        public Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RecordRelayAsync(KaleidoOutboxRelay relay, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<KaleidoOutboxStatus> GetStatusAsync(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

        public async Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> entries, CancellationToken cancellationToken = default)
        {
            if (transaction.Current is { } current)
            {
                db.Database.SetDbConnection(current.Connection);
                await db.Database.UseTransactionAsync(current, cancellationToken);
            }

            db.HostOutboxMessages.AddRange(entries.Select(entry => new HostOutboxMessage
            {
                EventId = entry.EventId,
                EventType = entry.EventType,
                Payload = entry.Payload,
            }));
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
