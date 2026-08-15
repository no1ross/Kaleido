using Kaleido.Process.Attributes;
using Kaleido.Samples.ECommerce.Steps;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Name = "process-cart",
    DisplayName = "Shopping Carts - Process Cart",
    Version = "1.0",
    Description = "Processes the shopping cart and starts an order.")]
[AvailableUntil(typeof(SubmitOrderStep))]
[AvailableAfter(typeof(AddItemToCartStep))]
[Repeatable]
public sealed record ProcessCartStep
{
    public required Guid ShoppingCartId
    {
        get;
        init;
    }

    public required Guid CustomerId
    {
        get;
        init;
    }
}