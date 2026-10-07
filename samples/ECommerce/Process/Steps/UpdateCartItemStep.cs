using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;
using Kaleido.Samples.ECommerce.Steps;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    DisplayName = "Shopping Carts - Update Item Quantity",
    Description = "Changes the quantity of an item in the shopping cart.",
    Version = "1.0")]
[AvailableAfter<AddItemToCartStep>]
[AvailableUntil<SubmitOrderStep>]
[Repeatable]
public sealed record UpdateCartItemStep : IProcessStep
{
    [Required]
    public required Guid ShoppingCartId { get; init; }

    [Required]
    public required Guid ShoppingCartItemId { get; init; }

    [Range(1, 999)]
    public required int Quantity { get; init; }
}
