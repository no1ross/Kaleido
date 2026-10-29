using Kaleido.Provider.SQLite.Entities;
using Kaleido.Storage;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

// The storage of Kaleido's built-in outbox, in the OutboxEvents table: entries are added inside the
// transition's transaction when SqliteKaleidoTransactions opened one on this DbContext (state and
// events commit together), on their own otherwise, and leased to the relay of this instance.
internal sealed class SqliteKaleidoOutboxStore(
    SqliteKaleidoContextDbContext dbContext,
    KaleidoServiceOptions serviceOptions,
    TimeProvider time)
    : IKaleidoOutboxStore
{
    internal const int MaxErrorLength = 2000;

    public async Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        dbContext.OutboxEvents.AddRange(events.Select(x => x.ToEntity()));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Each statement is atomic in SQLite (writers are serialized): the UPDATE re-checks that an entry
    // is still unleased, so two instances claiming at once never lease the same entry, and each
    // reads back only the entries it leased itself.
    public async Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcTicks;
        var leaseUntil = now + lease.Ticks;

        var due = await dbContext.OutboxEvents
            .AsNoTracking()
            .Where(x => !x.IsParked && x.NextAttemptUtc <= now && x.LeaseUntil <= now)
            .OrderBy(x => x.OccurredUtc)
            .Select(x => x.EventId)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return [];
        }

        await dbContext.OutboxEvents
            .Where(x => due.Contains(x.EventId) && !x.IsParked && x.LeaseUntil <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.LeaseOwner, Owner)
                    .SetProperty(x => x.LeaseUntil, leaseUntil),
                cancellationToken);

        var claimed = await dbContext.OutboxEvents
            .AsNoTracking()
            .Where(x => due.Contains(x.EventId) && x.LeaseOwner == Owner && x.LeaseUntil == leaseUntil)
            .OrderBy(x => x.OccurredUtc)
            .ToListAsync(cancellationToken);

        return [.. claimed.Select(x => x.ToOutboxEvent())];
    }

    // Only while this instance still holds the lease.
    public async Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcTicks;

        await dbContext.OutboxEvents
            .Where(x => x.EventId == eventId && x.LeaseOwner == Owner && x.LeaseUntil > now)
            .ExecuteDeleteAsync(cancellationToken);
    }

    // Only while this instance still holds the lease; releases it.
    public async Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow().UtcTicks;
        var lastError = errorMessage.Length > MaxErrorLength ? errorMessage[..MaxErrorLength] : errorMessage;
        var parked = retryAtUtc is null;
        var retryAt = retryAtUtc?.UtcTicks ?? now;

        await dbContext.OutboxEvents
            .Where(x => x.EventId == eventId && x.LeaseOwner == Owner && x.LeaseUntil > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.LastError, lastError)
                    .SetProperty(x => x.NextAttemptUtc, retryAt)
                    .SetProperty(x => x.IsParked, parked)
                    .SetProperty(x => x.LeaseOwner, (string?)null)
                    .SetProperty(x => x.LeaseUntil, 0L),
                cancellationToken);
    }

    // This instance's identity as lease owner.
    private string Owner => serviceOptions.InstanceId.ToString("N");
}
