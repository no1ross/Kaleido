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
    public required Guid CustomerId
    {
        get;
        init;
    }
}