namespace Kaleido.Provider.SQLite.Entities;

// A relay instance's heartbeat in the outbox it drains (#552). Times are UTC ticks.
internal sealed class OutboxRelayEntity
{
    // The relay host's instance id (Guid "N" format).
    public string InstanceId
    {
        get;
        set;
    } = string.Empty;

    public string ServiceName
    {
        get;
        set;
    } = string.Empty;

    public string Publisher
    {
        get;
        set;
    } = string.Empty;

    public long SeenUtc
    {
        get;
        set;
    }
}
