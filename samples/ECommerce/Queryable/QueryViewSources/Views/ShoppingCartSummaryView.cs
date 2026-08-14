namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record ShoppingCartSummaryView
{
    public Guid? ParticipantProcessId { get; set; } = null;
    public Guid? ShoppingCartId { get; set; } = null;
    public Guid? CustomerId { get; set; } = null;
    public int ItemCount { get; set; } = 0;
    public decimal TotalPrice { get; set; } = 0;
}
