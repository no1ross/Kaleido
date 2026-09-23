using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable.Attributes;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

[QueryContext(
    Name = "shopping-carts",
    DisplayName = "Shopping Carts",
    Version = "1.0.0",
    Source = "E-Commerce Catalog")]
public sealed class ShoppingCartQueryContext
{
    [Key]
    public Guid ShoppingCartId { get; init; }
    public Guid ShoppingCartItemId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? ProcessId { get; set; }
    public Guid ProductId { get; init; }
    public string ProductName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsActive { get; set; }
}
