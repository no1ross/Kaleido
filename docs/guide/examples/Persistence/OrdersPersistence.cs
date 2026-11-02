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
// context store, the outbox and the transaction share it.
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

public sealed class OutboxMessage
{
    public Guid EventId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public DateTimeOffset OccurredUtc { get; set; }
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
// Your outbox: Kaleido hands it every event; a state transition's event inside the transaction.
public sealed class OrdersOutbox(OrdersDbContext db) : IKaleidoOutbox
{
    public async Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken)
    {
        db.OutboxMessages.AddRange(events.Select(x => new OutboxMessage
        {
            EventId = x.EventId,
            EventType = x.EventType,
            Payload = x.Payload,
            OccurredUtc = x.OccurredUtc,
        }));

        await db.SaveChangesAsync(cancellationToken);
    }

    // Optional: after the commit, wake your dispatcher instead of waiting for its next poll.
    public Task CommittedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
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
            .AddOutbox<OrdersOutbox>()
            .AddTransactions<OrdersTransactions>()
            // This example keeps its idempotency records in memory; a production host registers a
            // durable IKaleidoIdempotencyStore (AddIdempotencyStore<T>(), AddSqliteIdempotencyStore).
            .AddInMemoryStores();
        // end snippet
    }
}
