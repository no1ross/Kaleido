
using Kaleido.Queryable;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

public sealed record CustomerQueryContext : IQueryContext
{
    public Guid CustomerId
    {
        get;
        init;
    }

    public string FirstName
    {
        get;
        init;
    } = string.Empty;

    public string LastName
    {
        get;
        init;
    } = string.Empty;

    public string Email
    {
        get;
        init;
    } = string.Empty;

    public bool IsActive
    {
        get;
        init;
    }
}