using Kaleido.Process.Attributes;
using Kaleido.Samples.PriorAuth.CodeSet;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    Name = "CaptureRequestedService",
    DisplayName = "Radiology - Capture Requested Service",
    Description = "Adds a requested service to the current prior authorization.",
    Version = "1.0.0")]
[AvailableAfter(typeof(CaptureMemberStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record CaptureRequestedServiceStep
{
    [Required]
    [StringLength(50)]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    public ProcedureCodeSystem CodeSystem { get; init; }
}
