namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Capture Servicing Provider",
    Description = "Placeholder for servicing provider selection in the current prior authorization flow.",
    Version = "1.0.0")]
[AvailableAfter<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record CaptureServicingProviderStep : IProcessStep;
