
using Kaleido.Queryable.Attributes;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

[QueryContext(
    Name = "customers",
    DisplayName = "Customers",
    Version = "1.0.0",
    Source = "E-Commerce Catalog")]
public sealed record CustomerQueryContext
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