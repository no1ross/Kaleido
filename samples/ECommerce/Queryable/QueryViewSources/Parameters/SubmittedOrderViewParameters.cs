using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record SubmittedOrderViewParameters : IQueryParameters
{
    [Required]
    public Guid? ProcessId
    {
        get;
        init;
    }

    [Required]
    public Guid? CustomerId
    {
        get;
        init;
    }

    public Guid? OrderId
    {
        get;
        init;
    }
}
