namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Confirm CT Instead Of MRI",
    Description = "Captures the current CT recommendation branch placeholder.",
    Version = "1.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record ConfirmCtInsteadOfMriStep : IProcessStep;
