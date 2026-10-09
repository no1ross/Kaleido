using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[ProcessStep(
    DisplayName = "Intake - Validate Member",
    Description = "Confirms that the selected member exists in the system. " +
                  "Does not persist any data. Returns CaptureMember as the required next step.",
    Version = "1.0.0")]
// An entry point, like StartIntake and CaptureRequestedService: a caller may
// begin an intake with any of the three.
[AvailableUntil<CaptureMemberStep>]
[Repeatable]
public sealed record ValidateMemberStep : IProcessStep
{
    [Required]
    public Guid MemberId { get; init; }

    [Required]
    public Guid MemberEnrollmentId { get; init; }

    [Required]
    public DateOnly DateOfService { get; init; }
}
