using Kaleido.Process.Attributes;
using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;

[ProcessStep(
    Name = "CaptureRequestedService",
    DisplayName = "Intake - Capture Requested Service",
    Description = "Adds a requested service to the current prior authorization.",
    Version = "1.0.0")]
[DependsOnStep(typeof(CaptureMemberStep))]
[Repeatable]
public sealed record CaptureRequestedServiceStep
{
    public Guid? ProcedureCodeId { get; init; }

    [Required]
    [StringLength(50)]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    public ProcedureCodeSystem CodeSystem { get; init; }

    [Required]
    [StringLength(200)]
    public string Description { get; init; } = string.Empty;
}
