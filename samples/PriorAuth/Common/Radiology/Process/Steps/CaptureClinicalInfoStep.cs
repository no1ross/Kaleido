namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

/// <summary>
/// The clinical information for the request. The questions, their wording and options come from
/// the Configuration service (per plan, line of business, modality and procedure), so this is an
/// information step: its payload is the answers to the request presented with it.
/// </summary>
[ProcessStep(
    DisplayName = "Capture Clinical Information",
    Description = "Answers the clinical questions configured for the requested service.",
    Version = "1.0.0")]
[AvailableAfter<CaptureServicingProviderStep>]
[AvailableUntil<SubmitPriorAuthorizationStep>]
[Repeatable]
public sealed record CaptureClinicalInfoStep : IInformationStep
{
    public required string InformationRequestId { get; init; }

    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}
