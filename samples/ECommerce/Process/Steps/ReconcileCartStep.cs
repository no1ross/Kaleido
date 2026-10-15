using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;
using Kaleido.Samples.ECommerce.Steps;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Version = "1.0",
    DisplayName = "Shopping Carts - Reconcile Cart",
    Description =
        "Associates a customer with the current process.")]
[AvailableAfter<AddItemToCartStep>]
[AvailableUntil<SubmitOrderStep>]
public sealed record ReconcileCartStep : IProcessStep
{
    [Required]
    [Description("The customer to associate with the current process and its shopping cart.")]
    public required Guid CustomerId
    {
        get;
        init;
    }
}
