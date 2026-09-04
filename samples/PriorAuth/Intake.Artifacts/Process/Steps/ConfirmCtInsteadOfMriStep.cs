using Kaleido.Process.Attributes;
using Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[ProcessStep(
    Name = "ConfirmCtInsteadOfMri",
    DisplayName = "Intake - Confirm CT Instead Of MRI",
    Description = "Captures the current CT recommendation branch placeholder.",
    Version = "1.0.0")]
[AvailableAfter(typeof(CaptureRequestedServiceStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record ConfirmCtInsteadOfMriStep;
