using System.ComponentModel.DataAnnotations;
using Kaleido.Process;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    Name = "ValidateMember",
    DisplayName = "Radiology - Validate Member",
    Description = "Validates member eligibility for the current prior authorization. " +
                  "Requires the radiology intake to have been started. " +
                  "Checks that the member exists and has active enrollment for the date of service.",
    Version = "1.0.0")]
[AvailableAfter(typeof(StartRadiologyIntakeStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
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
