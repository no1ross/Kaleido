using Kaleido.Process.Attributes;
using Kaleido.Samples.ECommerce.Steps;
using System.ComponentModel.DataAnnotations;

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