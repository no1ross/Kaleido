using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Capture MRI Information",
    Description = "Captures MRI-specific information for the requested service.",
    Version = "1.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record CaptureMriInfoStep : IProcessStep
{
    [Required]
    public MriBodyPart BodyPart { get; init; }

    [Required]
    public Laterality Laterality { get; init; }

    [Required]
    public ContrastOption Contrast { get; init; }
}
