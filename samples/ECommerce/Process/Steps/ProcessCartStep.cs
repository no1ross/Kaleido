using System.ComponentModel;
using Kaleido.Processor;
using Kaleido.Samples.ECommerce.Steps;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    DisplayName = "Shopping Carts - Process Cart",
    Version = "1.0",
    Description = "Processes the shopping cart and starts an order.")]
[AvailableUntil<SubmitOrderStep>]
[AvailableAfter<AddItemToCartStep>]
[Repeatable]
public sealed record ProcessCartStep : IProcessStep
{
    [Description("The shopping cart to turn into an order.")]
    public required Guid ShoppingCartId
    {
        get;
        init;
    }

    [Description("The customer placing the order; must own the shopping cart.")]
    public required Guid CustomerId
    {
        get;
        init;
    }
}
