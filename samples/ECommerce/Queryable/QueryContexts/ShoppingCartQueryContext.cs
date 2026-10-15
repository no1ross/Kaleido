using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

public sealed class ShoppingCartQueryContext : IQueryContext
{
    [Key]
    [Description("The shopping cart this item belongs to.")]
    public Guid ShoppingCartId { get; init; }
    [Description("The unique ID of this cart item.")]
    public Guid ShoppingCartItemId { get; init; }
    [Description("The customer who owns the cart, if known.")]
    public Guid? CustomerId { get; init; }
    [Description("The process the cart belongs to, if any.")]
    public Guid? ProcessId { get; set; }
    [Description("The product in this cart item.")]
    public Guid ProductId { get; init; }
    [Description("The name of the product.")]
    public string ProductName { get; set; } = string.Empty;
    [Description("The supplier of the product.")]
    public string SupplierName { get; set; } = string.Empty;
    [Description("The product family the product belongs to.")]
    public string FamilyName { get; set; } = string.Empty;
    [Description("The product's model name.")]
    public string ModelName { get; set; } = string.Empty;
    [Description("The description of the product.")]
    public string Description { get; set; } = string.Empty;
    [Description("How many units of the product are in the cart.")]
    public int Quantity { get; set; }
    [Description("The price of one unit of the product.")]
    public decimal UnitPrice { get; set; }
    [Description("Whether the shopping cart is active.")]
    public bool IsActive { get; set; }
}
