using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Steps;

[ProcessStep(
    Name = "AcceptTermsAndConditions",
    DisplayName = "Accept Order Terms",
    Description = "Confirms acceptance of the terms and conditions required before an order can be submitted.",
    Version = "1.0")]
[AvailableUntil(typeof(SubmitOrderStep))]
[AvailableAfter(typeof(StartOrderStep))]
[Repeatable]
public sealed record AcceptTermsAndConditionsStep
{
    [Required]
    public required string OrderId { get; init; }

    [Required]
    public required bool Accepted { get; init; }

    [Required]
    public required DateTimeOffset AcceptedOn { get; init; }
}
