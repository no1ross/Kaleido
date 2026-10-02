namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[KaleidoAuthorization(Roles = "radiology")]
[ProcessStep(
    Name = "ConfirmCtInsteadOfMri",
    DisplayName = "Confirm CT Instead Of MRI",
    Description = "Captures the current CT recommendation branch placeholder.",
    Version = "1.0.0")]
[AvailableAfter(typeof(StartRadiologyIntakeStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record ConfirmCtInsteadOfMriStep;
