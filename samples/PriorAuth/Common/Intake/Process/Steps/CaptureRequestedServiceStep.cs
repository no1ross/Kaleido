using Kaleido.Process.Attributes;
using Kaleido.Samples.PriorAuth.CodeSet;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[ProcessStep(
    Name = "CaptureRequestedService",
    DisplayName = "Intake - Capture Requested Service",
    Description = "Validates the procedure code and determines which processor should handle this prior authorization request.",
    Version = "1.0.0")]
public sealed record CaptureRequestedServiceStep
{
    [Required]
    [StringLength(50)]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    public ProcedureCodeSystem CodeSystem { get; init; }
}
