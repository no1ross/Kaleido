using Kaleido.Process.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[ProcessStep(
    Name = "CaptureMriInfo",
    DisplayName = "Intake - Capture MRI Information",
    Description = "Captures MRI-specific information for the requested service.",
    Version = "1.0.0")]
[AvailableAfter(typeof(CaptureRequestedServiceStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record CaptureMriInfoStep
{
    [Required]
    public MriBodyPart BodyPart { get; init; }

    [Required]
    public Laterality Laterality { get; init; }

    [Required]
    public ContrastOption Contrast { get; init; }
}
