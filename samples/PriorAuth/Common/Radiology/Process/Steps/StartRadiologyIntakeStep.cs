using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[KaleidoAuthorization(Roles = "radiology")]
[ProcessStep(
    Name = "StartRadiologyIntake",
    DisplayName = "Start Radiology Intake",
    Description = "Initializes a radiology prior authorization from intake handoff data. " +
                  "Accepts member and procedure information collected by the intake processor.",
    Version = "1.0.0")]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
public sealed record StartRadiologyIntakeStep
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
