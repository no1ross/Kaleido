namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

/// <summary>
/// Submits the request. Its payload is the caller's answers to the attestation configured for
/// submission (presented by Review); the request is submitted only when the caller attests.
/// Kaleido records the submission; it never decides approval.
/// </summary>
[ProcessStep(
    DisplayName = "Submit Prior Authorization",
    Description = "Submits the reviewed request once the caller confirms the configured attestation.",
    Version = "1.0.0",
    Destructive = true,
    External = true)]
[AvailableAfter<ReviewPriorAuthorizationStep>]
public sealed record SubmitPriorAuthorizationStep : IInformationStep
{
    public required string InformationRequestId { get; init; }

    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}
