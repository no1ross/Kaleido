using Kaleido.Process.Execution;
using Kaleido.Queryable.Http.Client;
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
        CancellationToken cancellationToken = default)
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
                return ProcessStepHandlerResult.Failure(eligibility.FailureMessage!);
            }

            return ProcessStepHandlerResult.Success(
                requiredStep: nameof(CaptureMemberStep).Replace("Step", string.Empty));
        }
        catch (KaleidoQueryableClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                Process.Messages.RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
