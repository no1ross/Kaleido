using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;
using Kaleido.Samples.ECommerce.Steps;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    DisplayName = "Shopping Carts - Remove Item from Cart",
    Description = "Removes an existing item from the shopping cart.",
    Version = "1.0")]
[AvailableAfter<AddItemToCartStep>]
[AvailableUntil<SubmitOrderStep>]
[Repeatable]
public sealed record RemoveCartItemStep : IProcessStep
{
    [Required]
    public required Guid ShoppingCartId { get; init; }

    [Required]
    public required Guid ShoppingCartItemId { get; init; }
}

