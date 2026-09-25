using System.ComponentModel.DataAnnotations;
using Kaleido.Process;
using Kaleido.Samples.ECommerce.Process.Steps;

namespace Kaleido.Samples.ECommerce.Steps;

[ProcessStep(
    Name = "submit-order",
    DisplayName = "Orders - Submit Order",
    Description = "Submits the completed order for processing.",
    Version = "1.0")]
[AvailableAfter(typeof(ProcessCartStep))]
//[AvailableUntil(typeof(SubmitOrderStep))]
//[DependsOnStep(typeof(SubmitBillingStep))]
//[DependsOnStep(typeof(AcceptTermsAndConditionsStep))]
public sealed record SubmitOrderStep
{
    [Required]
    public required Guid CustomerId { get; init; }
    [Required]
    public required Guid OrderId { get; init; }
}
