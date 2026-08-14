using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Name = "RemoveCartItem",
    DisplayName = "Shopping Cart - Remove Item from Cart",
    Description = "Removes an existing item from the shopping cart.",
    Version = "1.0")]
[DependsOnStep(typeof(AddItemToCartStep))]
[Repeatable]
public sealed record RemoveCartItemStep
{
    [Required]
    public required Guid ShoppingCartId { get; init; }

    [Required]
    public required Guid ShoppingCartItemId { get; init; }
}


