using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    "UpdateQuantity",
    "Updates an item quantity in the cart.",
    "1.0")]
[DependsOnStep(typeof(AddItemToCartStep))]
public sealed record UpdateQuantityStep
{
    [Required]
    public required string CartId { get; init; }

    [Required]
    public required string ItemId { get; init; }

    [Range(1, 999)]
    public required int Quantity { get; init; }
}

public sealed record UpdateQuantityResponse
{
    public required string CartId { get; init; }

    public required string ItemId { get; init; }

    public required int Quantity { get; init; }

    public required decimal CartTotal { get; init; }
}