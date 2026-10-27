using Kaleido.Storage;
namespace Kaleido.Provider.SQLite.Entities;

internal sealed class IdempotentResultEntity
{
    public string Key
    {
        get;
        set;
    } = string.Empty;

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

    public IdempotentResultState State
    {
        get;
        set;
    }

    public DateTimeOffset LeaseUntilUtc
    {
        get;
        set;
    }

    public int StatusCode
    {
        get;
        set;
    }

    public string Response
    {
        get;
        set;
    } = string.Empty;

    public DateTimeOffset ExpiresUtc
    {
        get;
        set;
    }
}