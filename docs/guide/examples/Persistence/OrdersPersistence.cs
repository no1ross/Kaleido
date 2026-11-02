using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Guide.Examples.Persistence;

// snippet: persistence-dbcontext
// One DbContext for the process state and the outbox: registered scoped, one per request, so the
// context store, the outbox store and the transaction share it.
public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<ProcessRow> Processes => Set<ProcessRow>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A process is stored under its processor, version and id: services and versions may share it.
        modelBuilder.Entity<ProcessRow>().HasKey(x => new { x.ProcessorName, x.ProcessorVersion, x.ProcessId });
        modelBuilder.Entity<OutboxMessage>().HasKey(x => x.EventId);
    }
}

public sealed class ProcessRow
{
    public string ProcessorName { get; set; } = string.Empty;

    // Empty when the processor is unversioned: a key column is never null.
    public string ProcessorVersion { get; set; } = string.Empty;

    public Guid ProcessId { get; set; }

    public long Version { get; set; }

    public string State { get; set; } = string.Empty;
}

// Times as UTC ticks, so due and expired entries are found with plain comparisons.
public sealed class OutboxMessage
{
    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string? ServiceVersion { get; set; }

    public string Payload { get; set; } = string.Empty;

    public long OccurredUtc { get; set; }

    public int Attempts { get; set; }

    public long NextAttemptUtc { get; set; }

    public string? LastError { get; set; }

    public bool IsParked { get; set; }

    public string? LeaseOwner { get; set; }

    public long LeaseUntil { get; set; }
}
// end snippet

// snippet: persistence-context-store
// Process state only, saved conditionally: a stale version is a conflict (409), never an overwrite.
// Every query matches the whole key - processor, version and id - so another service's or version's
// process under the same id (a hand-off) is never read or overwritten.
public sealed class OrdersContextStore(OrdersDbContext db) : IKaleidoContextStore
{
    public async Task<ProcessorContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken) =>
        await ByKey(db.Processes.AsNoTracking(), key).SingleOrDefaultAsync(cancellationToken) is { } row
            ? JsonSerializer.Deserialize<ProcessorContext>(row.State)
            : null;

    public async Task<ProcessorContext> SaveAsync(ProcessorContext context, CancellationToken cancellationToken)
    {
        var saved = context with { Version = context.Version + 1 };
        var state = JsonSerializer.Serialize(saved);

        if (context.Version == 0)
        {
            if (await ByKey(db.Processes, context.Key).AnyAsync(cancellationToken))
            {
                throw new KaleidoConcurrencyException(context.ProcessId);
            }

            db.Processes.Add(new ProcessRow
            {
                ProcessorName = context.ProcessorName,
                ProcessorVersion = context.ProcessorVersion ?? string.Empty,
                ProcessId = context.ProcessId,
                Version = saved.Version,
                State = state,
            });
            await db.SaveChangesAsync(cancellationToken);
            return saved;
        }

        var updated = await ByKey(db.Processes, context.Key)
            .Where(x => x.Version == context.Version)
            .ExecuteUpdateAsync(x => x.SetProperty(p => p.Version, saved.Version).SetProperty(p => p.State, state), cancellationToken);

        return updated == 1 ? saved : throw new KaleidoConcurrencyException(context.ProcessId);
    }

    private static IQueryable<ProcessRow> ByKey(IQueryable<ProcessRow> rows, ProcessKey key)
    {
        var version = key.ProcessorVersion ?? string.Empty;

        return rows.Where(x => x.ProcessorName == key.ProcessorName && x.ProcessorVersion == version && x.ProcessId == key.ProcessId);
    }
}
// end snippet

// snippet: persistence-outbox
// The outbox on the same DbContext: every event the service writes - a state transition's inside its
// transaction - and the relay's claims, each leased to one relay instance at a time.
public sealed class OrdersOutboxStore(OrdersDbContext db, KaleidoServiceOptions service, TimeProvider time) : IKaleidoOutboxStore
{
    private string Owner => service.InstanceId.ToString("N");

    public async Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken)
    {
        db.OutboxMessages.AddRange(events.Select(x => new OutboxMessage
        {
            EventId = x.EventId,
            EventType = x.EventType,
            ServiceName = x.ServiceName,
            ServiceVersion = x.ServiceVersion,
            Payload = x.Payload,
            OccurredUtc = x.OccurredUtc.UtcTicks,
            NextAttemptUtc = x.NextAttemptUtc.UtcTicks,
        }));

        await db.SaveChangesAsync(cancellationToken);
    }

    // A compare-and-set: the UPDATE re-checks that each entry is still unleased, so two relays claiming
    // at once never get the same one; each reads back only what it leased.
    public async Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcTicks;
        var until = now + lease.Ticks;
        var due = await db.OutboxMessages.AsNoTracking()
            .Where(x => !x.IsParked && x.NextAttemptUtc <= now && x.LeaseUntil <= now)
            .OrderBy(x => x.OccurredUtc).Select(x => x.EventId).Take(maxCount)
            .ToListAsync(cancellationToken);

        await db.OutboxMessages
            .Where(x => due.Contains(x.EventId) && !x.IsParked && x.LeaseUntil <= now)
            .ExecuteUpdateAsync(x => x.SetProperty(m => m.LeaseOwner, Owner).SetProperty(m => m.LeaseUntil, until), cancellationToken);

        var claimed = await db.OutboxMessages.AsNoTracking()
            .Where(x => due.Contains(x.EventId) && x.LeaseOwner == Owner && x.LeaseUntil == until)
            .OrderBy(x => x.OccurredUtc)
            .ToListAsync(cancellationToken);

        return [.. claimed.Select(x => new KaleidoOutboxEvent
        {
            EventId = x.EventId,
            EventType = x.EventType,
            ServiceName = x.ServiceName,
            ServiceVersion = x.ServiceVersion,
            Payload = x.Payload,
            OccurredUtc = new DateTimeOffset(x.OccurredUtc, TimeSpan.Zero),
            Attempts = x.Attempts,
            NextAttemptUtc = new DateTimeOffset(x.NextAttemptUtc, TimeSpan.Zero),
            LastError = x.LastError,
        })];
    }

    // Both only while this instance still holds the lease.
    public Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken) =>
        Leased(eventId).ExecuteDeleteAsync(cancellationToken);

    public Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken) =>
        Leased(eventId).ExecuteUpdateAsync(
            x => x.SetProperty(m => m.Attempts, m => m.Attempts + 1)
                .SetProperty(m => m.LastError, errorMessage)
                .SetProperty(m => m.NextAttemptUtc, (retryAtUtc ?? time.GetUtcNow()).UtcTicks)
                .SetProperty(m => m.IsParked, retryAtUtc == null)
                .SetProperty(m => m.LeaseOwner, (string?)null)
                .SetProperty(m => m.LeaseUntil, 0L),
            cancellationToken);

    private IQueryable<OutboxMessage> Leased(Guid eventId)
    {
        var now = time.GetUtcNow().UtcTicks;

        return db.OutboxMessages.Where(x => x.EventId == eventId && x.LeaseOwner == Owner && x.LeaseUntil > now);
    }
}
// end snippet

// snippet: persistence-transactions
// One database transaction per state transition, on the shared DbContext: the state and its event
// commit together, or neither is kept.
public sealed class OrdersTransactions(OrdersDbContext db) : IKaleidoTransactions
{
    public async Task<IKaleidoTransaction> BeginAsync(CancellationToken cancellationToken) =>
        new Transaction(await db.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Transaction(IDbContextTransaction transaction) : IKaleidoTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();   // without a commit: rolled back
    }
}
// end snippet

public static class OrdersPersistence
{
    public static IKaleidoBuilder AddOrdersPersistence(IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        // snippet: persistence-registration
        services.AddDbContext<OrdersDbContext>(o => o.UseSqlite(connectionString));   // scoped

        return services.AddKaleido(configuration, new() { ServiceName = "orders", Assemblies = [typeof(OrdersContextStore).Assembly] })
            .AddContextStore<OrdersContextStore>()
            .AddOutboxStore<OrdersOutboxStore>()
            .AddTransactions<OrdersTransactions>()
            // This example keeps its idempotency records in memory; a production host registers a
            // durable IKaleidoIdempotencyStore (AddIdempotencyStore<T>(), AddSqliteIdempotencyStore).
            .AddInMemoryStores();
        // end snippet
    }
}
