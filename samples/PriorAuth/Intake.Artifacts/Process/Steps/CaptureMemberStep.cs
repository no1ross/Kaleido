using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;

[ProcessStep(
    Name = "CaptureMember",
    DisplayName = "Intake - Capture Member",
    Description = "Creates or updates the prior authorization with the selected member.",
    Version = "1.0.0")]
public sealed record CaptureMemberStep
{
    [Required]
    public Guid MemberId { get; init; }

    [Required]
    public Guid MemberEnrollmentId { get; init; }

    [Required]
    public DateOnly DateOfService { get; init; }
}
