namespace Kaleido.Provider.SQLite.Entities;

// One outbox entry. Times are UTC ticks, so due and expired entries are found with plain comparisons.
internal sealed class OutboxEventEntity
{
    public Guid EventId
    {
        get;
        set;
    }

    public string EventType
    {
        get;
        set;
    } = string.Empty;

    public string Payload
    {
        get;
        set;
    } = string.Empty;

    public long OccurredUtc
    {
        get;
        set;
    }

    public int Attempts
    {
        get;
        set;
    }

    public long NextAttemptUtc
    {
        get;
        set;
    }

    public string? LastError
    {
        get;
        set;
    }

    public bool IsParked
    {
        get;
        set;
    }

    // The instance holding the lease (its InstanceId) and until when; 0 = not leased.
    public string? LeaseOwner
    {
        get;
        set;
    }

    public long LeaseUntil
    {
        get;
        set;
    }
}
