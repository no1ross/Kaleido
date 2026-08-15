using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Process.Steps;

[ProcessStep(
    Name = "ReconcileCartOwnership",
    Version = "1.0",
    DisplayName = "Shopping Cart - Reconcile    Ownership",
    Description =
        "Associates a customer with the current process.")]
public sealed record ReconcileCartOwnershipStep
{
    [Required]
    public required Guid CustomerId
    {
        get;
        init;
    }
}