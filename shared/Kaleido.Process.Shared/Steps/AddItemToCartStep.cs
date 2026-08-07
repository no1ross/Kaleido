using Kaleido.Process.Attributes;
using Kaleido.Process.Shared.Data;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    Name = "AddItemToCart",
    DisplayName = "Add Item to Cart",
    Description = "Adds one or more products to the shopping cart.",
    Version = "1.0")]
[Repeatable]
public sealed record AddItemToCartStep
{
    [Required]
    [StringLength(50)]
    public required string CartId { get; init; }

    [Required]
    [MinLength(1)]
    public required IReadOnlyCollection<CartItemRequest> Items { get; init; }

    public string? Notes { get; init; }
}

public sealed record CartItemRequest
{
    [Required]
    [StringLength(100)]
    public required string ItemId
    {
        get;
        init;
    }

    [Required]
    [StringLength(250)]
    public required string Description
    {
        get;
        init;
    }

    [Required]
    public required CartItemType ItemType
    {
        get;
        init;
    }

    [Range(1, 999)]
    public required int Quantity
    {
        get;
        init;
    }

    [Range(typeof(decimal), "0.01", "999999.99")]
    public required decimal UnitPrice
    {
        get;
        init;
    }
}
