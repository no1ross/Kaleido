using Kaleido.Process.Attributes;
using Kaleido.Samples.ECommerce.Steps;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Name = "update-cart-item",
    DisplayName = "Shopping Carts - Update Item Quantity",
    Description = "Changes the quantity of an item in the shopping cart.",
    Version = "1.0")]
[AvailableAfter(typeof(AddItemToCartStep))]
[AvailableUntil(typeof(SubmitOrderStep))]
[Repeatable]
public sealed record UpdateCartItemStep
{
    [Required]
    public required Guid ShoppingCartId { get; init; }

    [Required]
    public required Guid ShoppingCartItemId { get; init; }

    [Range(1, 999)]
    public required int Quantity { get; init; }
}
