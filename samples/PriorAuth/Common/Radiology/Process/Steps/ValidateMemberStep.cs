using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Validate Member",
    Description = "Validates member eligibility for the current prior authorization. " +
                  "Requires the radiology intake to have been started. " +
                  "Checks that the member exists and has active enrollment for the date of service.",
    Version = "1.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
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
