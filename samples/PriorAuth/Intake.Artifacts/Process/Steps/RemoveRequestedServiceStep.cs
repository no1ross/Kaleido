using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;

[ProcessStep(
    Name = "RemoveRequestedService",
    DisplayName = "Intake - Remove Requested Service",
    Description = "Removes a requested service from the current prior authorization.",
    Version = "1.0.0")]
[AvailableAfter(typeof(CaptureRequestedServiceStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record RemoveRequestedServiceStep
{
    [Required]
    public Guid PriorAuthorizationRequestedServiceId { get; init; }
}
