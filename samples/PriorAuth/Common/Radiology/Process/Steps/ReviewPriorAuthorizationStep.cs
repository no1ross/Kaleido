namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Review Prior Authorization",
    Description = "Returns the whole request (member, requested services with their details, providers and clinical answers) to read back to the caller before submitting. Then the caller confirms the configured attestation to submit.",
    Version = "1.0.0")]
[AvailableAfter<CaptureServicingProviderStep>]
[AvailableUntil<SubmitPriorAuthorizationStep>]
[Repeatable]
public sealed record ReviewPriorAuthorizationStep : IProcessStep;
