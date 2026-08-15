using Kaleido.Process.Attributes;
using Kaleido.Samples.ECommerce.Steps;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Name = "remove-cart-item",
    DisplayName = "Shopping Carts - Remove Item from Cart",
    Description = "Removes an existing item from the shopping cart.",
    Version = "1.0")]
[AvailableAfter(typeof(AddItemToCartStep))]
[AvailableUntil(typeof(SubmitOrderStep))]
[Repeatable]
public sealed record RemoveCartItemStep
{
    [Required]
    public required Guid ShoppingCartId { get; init; }

    [Required]
    public required Guid ShoppingCartItemId { get; init; }
}


