using Kaleido.Process.Attributes;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;

[ProcessStep(
    Name = "CaptureServicingProvider",
    DisplayName = "Intake - Capture Servicing Provider",
    Description = "Placeholder for servicing provider selection in the current prior authorization flow.",
    Version = "1.0.0")]
[AvailableAfter(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record CaptureServicingProviderStep;
