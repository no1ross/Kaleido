using Kaleido.Processor;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

/// <summary>
/// The MRI details (body part, laterality, contrast). Which questions are asked, their wording and
/// their options come from the Configuration service per plan and modality, so this is an
/// information step: its payload is the answers to the request presented with it.
/// </summary>
[ProcessStep(
    DisplayName = "Capture MRI Information",
    Description = "Answers the MRI questions configured for the requested service.",
    Version = "2.0.0")]
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
public sealed record CaptureMriInfoStep : IInformationStep
{
    public required string InformationRequestId { get; init; }

    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}
