using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;
using Kaleido.Samples.ECommerce.Process.Steps;

namespace Kaleido.Samples.ECommerce.Steps;

[ProcessStep(
    DisplayName = "Orders - Submit Order",
    Description = "Submits the completed order for processing.",
    Version = "1.0")]
[AvailableAfter<ProcessCartStep>]
//[AvailableUntil<SubmitOrderStep>]
//[DependsOn<SubmitBillingStep>]
//[DependsOn<AcceptTermsAndConditionsStep>]
public sealed record SubmitOrderStep : IProcessStep
{
    [Required]
    public required Guid CustomerId { get; init; }
    [Required]
    public required Guid OrderId { get; init; }
}
