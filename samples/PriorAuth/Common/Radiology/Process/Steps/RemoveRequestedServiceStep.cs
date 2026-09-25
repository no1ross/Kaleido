using System.ComponentModel.DataAnnotations;
using Kaleido.Process;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    Name = "RemoveRequestedService",
    DisplayName = "Radiology - Remove Requested Service",
    Description = "Removes a requested service from the current prior authorization.",
    Version = "1.0.0")]
[AvailableAfter(typeof(StartRadiologyIntakeStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record RemoveRequestedServiceStep
{
    [Required]
    public Guid PriorAuthorizationRequestedServiceId { get; init; }
}
