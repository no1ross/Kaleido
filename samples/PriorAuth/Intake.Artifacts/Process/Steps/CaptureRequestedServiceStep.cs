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
    [Required]
    [StringLength(50)]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    public ProcedureCodeSystem CodeSystem { get; init; }
}

[ProcessStep(
    Name = "CaptureMriInfo",
    DisplayName = "Intake - Capture MRI Information",
    Description = "Captures MRI-specific information for the requested service.",
    Version = "1.0.0")]
[DependsOnStep(typeof(CaptureRequestedServiceStep))]
public sealed record CaptureMriInfoStep
{
    [Required]
    public MriBodyPart BodyPart { get; init; }

    [Required]
    public Laterality Laterality { get; init; }

    [Required]
    public ContrastOption Contrast { get; init; }
}

[ProcessStep(
    Name = "ConfirmCtInsteadOfMri",
    DisplayName = "Intake - Confirm CT Instead Of MRI",
    Description = "Captures the current CT recommendation branch placeholder.",
    Version = "1.0.0")]
[DependsOnStep(typeof(CaptureRequestedServiceStep))]
public sealed record ConfirmCtInsteadOfMriStep;
