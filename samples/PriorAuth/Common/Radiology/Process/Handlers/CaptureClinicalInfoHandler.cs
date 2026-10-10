using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// Keeps the clinical answers with their questions for review. Kaleido has already checked they
/// fit the questions; what they mean clinically is not Radiology's or Kaleido's decision here.
/// </summary>
public sealed class CaptureClinicalInfoHandler(
    ConfiguredInformationRequests informationRequests)
    : IProcessStepHandler<CaptureClinicalInfoStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureClinicalInfoStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        await informationRequests.RecordAnswersAsync(context.ProcessId, nameof(CaptureClinicalInfoStep), processStep, cancellationToken);

        return ProcessStepHandlerResult.Success<ReviewPriorAuthorizationStep>();
    }
}