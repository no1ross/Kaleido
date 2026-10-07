using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[ProcessStep(
    DisplayName = "Intake - Capture Member",
    Description = "Records member information against the intake session for auditing and correlation.",
    Version = "1.0.0")]
[AvailableAfter<ValidateMemberStep>]
public sealed record CaptureMemberStep : IProcessStep
{
    [Required]
    public Guid MemberId { get; init; }

    [Required]
    public Guid MemberEnrollmentId { get; init; }

    [Required]
    public DateOnly DateOfService { get; init; }
}
