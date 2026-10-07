using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Remove Requested Service",
    Description = "Removes a requested service from the current prior authorization.",
    Version = "1.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record RemoveRequestedServiceStep : IProcessStep
{
    [Required]
    public Guid PriorAuthorizationRequestedServiceId { get; init; }
}
