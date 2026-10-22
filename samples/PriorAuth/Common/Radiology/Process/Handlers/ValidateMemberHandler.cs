using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class ValidateMemberHandler(
    IMemberEligibilityService memberEligibilityService)
    : IProcessStepHandler<ValidateMemberStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        ValidateMemberStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var eligibility =
                await memberEligibilityService.ValidateAsync(
                    processStep.MemberId,
                    processStep.MemberEnrollmentId,
                    processStep.DateOfService,
                    context.ProcessId,
                    cancellationToken);

            if (!eligibility.Succeeded)
            {
                return ProcessStepHandlerResult.Reject(eligibility.FailureMessage!);
            }

            return ProcessStepHandlerResult.Success<CaptureMemberStep>();
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Reject(
                Process.Messages.RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
