using System.ComponentModel;
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
    [Description("The customer submitting the order.")]
    public required Guid CustomerId { get; init; }
    [Required]
    [Description("The order to submit.")]
    public required Guid OrderId { get; init; }
}
