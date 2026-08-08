using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Steps;

[ProcessStep(
    Name = "SubmitOrder",
    DisplayName = "Submit Order",
    Description = "Submits the completed order for processing.",
    Version = "1.0")]
[DependsOnStep(typeof(SubmitBillingStep))]
[DependsOnStep(typeof(AcceptTermsAndConditionsStep))]
public sealed record SubmitOrderStep
{
    [Required]
    public required string OrderId { get; init; }

    [StringLength(250)]
    public string? Comments { get; init; }
}
