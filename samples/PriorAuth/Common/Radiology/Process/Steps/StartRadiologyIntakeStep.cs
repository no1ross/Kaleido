using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Auth;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

// Handoff entry: only callable by a service (Intake) on behalf of a user who
// holds the radiology role. Never called directly by a consumer.
[KaleidoAuthorization(Roles = "radiology", Policy = DevAuthPolicies.InternalCaller)]
[ProcessStep(
    DisplayName = "Start Radiology Intake",
    Description = "Initializes a radiology prior authorization from intake handoff data. " +
                  "Accepts member and procedure information collected by the intake processor.",
    Version = "1.0.0")]
[AvailableUntil<CaptureRequestingProviderStep>]
public sealed record StartRadiologyIntakeStep : IProcessStep
{
    public Guid? MemberId { get; init; }

    public Guid? MemberEnrollmentId { get; init; }

    public DateOnly? DateOfService { get; init; }

    [Required]
    [StringLength(50)]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    public ProcedureCodeSystem CodeSystem { get; init; }
}
