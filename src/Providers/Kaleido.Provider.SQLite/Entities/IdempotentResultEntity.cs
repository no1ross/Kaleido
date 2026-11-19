namespace Kaleido.Provider.SQLite.Entities;

internal sealed class IdempotentResultEntity
{
    public string Key
    {
        get;
        set;
    } = string.Empty;

    // The processor and version the key ran on (#552): stored for operators, not part of the key.
    public string ProcessorName
    {
        get;
        set;
    } = string.Empty;

    public string? ProcessorVersion
    {
        get;
        set;
    }

    // The admission's id as text (Guid "D" format), the same form the store writes with raw SQL.
    public string AdmissionId
    {
        get;
        set;
    } = string.Empty;

    public string RequestHash
    {
        get;
        set;
    } = string.Empty;

    public IdempotencyState State
    {
        get;
        set;
    }

    public DateTimeOffset LeaseUntilUtc
    {
        get;
        set;
    }

    public string? ReplayFormat
    {
        get;
        set;
    }

    public byte[]? ReplayPayload
    {
        get;
        set;
    }

    public DateTimeOffset ExpiresUtc
    {
        get;
        set;
    }
}
