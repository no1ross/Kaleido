using System.ComponentModel;
using Kaleido.Queryable;
namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record ShoppingCartViewParameters : IQueryParameters
{
    [Description("The process whose shopping cart to show.")]
    public Guid? ProcessId { get; set; }
    [Description("The customer whose shopping cart to show.")]
    public Guid? CustomerId { get; set; }
}
