using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

/// <summary>
/// One round of the clinical review: keeps the answers, then asks the clinical question provider
/// what comes next. More questions require this step again with the next round; when the provider
/// is done, the request moves on to review. The provider decides; Radiology and Kaleido carry.
/// </summary>
public sealed class CaptureClinicalInfoHandler(
    RadiologyClinicalReview clinicalReview)
    : IProcessStepHandler<CaptureClinicalInfoStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureClinicalInfoStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await clinicalReview.ContinueAsync(context.ProcessId, processStep, cancellationToken) is { } next
                ? ProcessStepHandlerResult.RequireInformation<CaptureClinicalInfoStep>(next)
                : ProcessStepHandlerResult.Success<ReviewPriorAuthorizationStep>();
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                RadiologyProcessMessages.QueryableRequestFailed(ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED", ex.Message));
        }
    }
}