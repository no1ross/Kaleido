using System.ComponentModel.DataAnnotations;
using Kaleido.Process;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[KaleidoAuthorization(Roles = "radiology")]
[ProcessStep(
    Name = "CaptureMember",
    DisplayName = "Capture Member",
    Description = "Creates or updates the prior authorization with the selected member.",
    Version = "1.0.0")]
[AvailableAfter(typeof(StartRadiologyIntakeStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record CaptureMemberStep
{
    [Required]
    public Guid MemberId { get; init; }

    [Required]
    public Guid MemberEnrollmentId { get; init; }

    [Required]
    public DateOnly DateOfService { get; init; }
}
