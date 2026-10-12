namespace Kaleido.Provider.SQLite.Entities;

internal sealed class IdempotentResultEntity
{
    public string Key
    {
        get;
        set;
    } = string.Empty;

    public string RequestHash
    {
        get;
        set;
    } = string.Empty;

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