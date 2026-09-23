using Kaleido.Process.Attributes;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    Name = "ConfirmCtInsteadOfMri",
    DisplayName = "Radiology - Confirm CT Instead Of MRI",
    Description = "Captures the current CT recommendation branch placeholder.",
    Version = "1.0.0")]
[AvailableAfter(typeof(StartRadiologyIntakeStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record ConfirmCtInsteadOfMriStep;
