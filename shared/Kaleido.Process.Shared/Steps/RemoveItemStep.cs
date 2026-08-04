using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Process.Shared.Steps;

[ProcessStep(
    "RemoveItem",
    "Removes an item from the cart.",
    "1.0")]
[DependsOnStep(typeof(AddItemToCartStep))]
public sealed record RemoveItemStep
{
    [Required]
    public required string CartId { get; init; }

    [Required]
    public required string ItemId { get; init; }
}

public sealed record RemoveItemResponse
{
    public required string CartId { get; init; }

    public required string ItemId { get; init; }

    public required bool Removed { get; init; }

    public required int RemainingItems { get; init; }
}


