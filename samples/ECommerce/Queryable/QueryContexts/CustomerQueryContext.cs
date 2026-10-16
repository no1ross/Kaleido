
using System.ComponentModel;
using Kaleido.Queryable;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

public sealed record CustomerQueryContext : IQueryContext
{
    [Description("The unique ID of the customer.")]
    public Guid CustomerId
    {
        get;
        init;
    }

    [Description("The customer's first name.")]
    public string FirstName
    {
        get;
        init;
    } = string.Empty;

    [Description("The customer's last name.")]
    public string LastName
    {
        get;
        init;
    } = string.Empty;

    [Description("The customer's email address.")]
    public string Email
    {
        get;
        init;
    } = string.Empty;

    [Description("Whether the customer account is active.")]
    public bool IsActive
    {
        get;
        init;
    }
}
