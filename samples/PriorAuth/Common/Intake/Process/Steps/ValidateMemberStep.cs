using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[ProcessStep(
    Name = "ValidateMember",
    DisplayName = "Intake - Validate Member",
    Description = "Confirms that the selected member exists in the system. " +
                  "Does not persist any data. Returns CaptureMember as the required next step.",
    Version = "1.0.0")]
[AvailableAfter(typeof(StartIntakeStep))]
[AvailableUntil(typeof(CaptureMemberStep))]
[Repeatable]
public sealed record ValidateMemberStep
{
    [Required]
    public Guid MemberId { get; init; }

    [Required]
    public Guid MemberEnrollmentId { get; init; }

    [Required]
    public DateOnly DateOfService { get; init; }
}
