using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Capture Requested Service",
    Description = "Adds a requested service to the current prior authorization.",
    Version = "1.0.0")]
// Radiology is entered through StartRadiologyIntake (the member may have been
// captured in Intake), so adding services opens once the intake has started.
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record CaptureRequestedServiceStep : IProcessStep
{
    [Required]
    [StringLength(50)]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    public ProcedureCodeSystem CodeSystem { get; init; }
}
