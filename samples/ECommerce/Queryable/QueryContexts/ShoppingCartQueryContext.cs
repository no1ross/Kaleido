using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.ECommerce.Data.QueryContexts;

public sealed class ShoppingCartQueryContext : IQueryContext
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
