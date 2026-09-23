namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record ShoppingCartSummaryView
{
    public Guid? ProcessId { get; set; }
    public Guid? ShoppingCartId { get; set; }
    public Guid? CustomerId { get; set; }
    public int ItemCount { get; set; }
    public decimal TotalPrice { get; set; }
}
