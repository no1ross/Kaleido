using Kaleido.Queryable;
namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record ShoppingCartViewParameters : IQueryParameters
{
    public Guid? ProcessId { get; set; }
    public Guid? CustomerId { get; set; }
}
