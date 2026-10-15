using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record SubmittedOrderViewParameters : IQueryParameters
{
    [Required]
    [Description("The process whose submitted order to show.")]
    public Guid? ProcessId
    {
        get;
        init;
    }

    [Required]
    [Description("The customer who owns the order.")]
    public Guid? CustomerId
    {
        get;
        init;
    }

    [Description("The specific submitted order to show; optional.")]
    public Guid? OrderId
    {
        get;
        init;
    }
}
