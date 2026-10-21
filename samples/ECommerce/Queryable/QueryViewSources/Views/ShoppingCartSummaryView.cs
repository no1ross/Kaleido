using System.ComponentModel;

namespace Kaleido.Samples.ECommerce.Data.QueryViewSources.Views;

public sealed record ShoppingCartSummaryView
{
    [Description("The ID of the process the active cart belongs to; null if no active cart was found or the cart has no process.")]
    public Guid? ProcessId { get; set; }
    [Description("The unique ID of the active shopping cart; null if no active cart was found.")]
    public Guid? ShoppingCartId { get; set; }
    [Description("The unique ID of the customer who owns the cart; null if no active cart was found or the owner is unknown.")]
    public Guid? CustomerId { get; set; }
    [Description("The total number of units across all items in the active cart (the sum of the quantities); 0 if no active cart was found.")]
    public int ItemCount { get; set; }
    [Description("The total price of the active cart: the sum of quantity times unit price over all items; 0 if no active cart was found.")]
    public decimal TotalPrice { get; set; }
}
