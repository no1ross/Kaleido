namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Parameters;

public sealed record ShoppingCartViewParameters
{
    public Guid? ProcessId { get; set; }
    public Guid? CustomerId { get; set; }
}
