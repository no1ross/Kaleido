using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Capture Member",
    Description = "Creates or updates the prior authorization with the selected member.",
    Version = "1.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record CaptureMemberStep : IProcessStep
{
    [Required]
    public Guid MemberId { get; init; }

    [Required]
    public Guid MemberEnrollmentId { get; init; }

    [Required]
    public DateOnly DateOfService { get; init; }
}
