using System.ComponentModel.DataAnnotations;
using Kaleido.Process;
using Kaleido.Samples.ECommerce.Steps;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Name = "reconcile-cart",
    Version = "1.0",
    DisplayName = "Shopping Carts - Reconcile Cart",
    Description =
        "Associates a customer with the current process.")]
[AvailableAfter(typeof(AddItemToCartStep))]
[AvailableUntil(typeof(SubmitOrderStep))]
public sealed record ReconcileCartStep
{
    [Required]
    public required Guid CustomerId
    {
        get;
        init;
    }
}