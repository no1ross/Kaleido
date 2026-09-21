using Kaleido.Http.Client.Queryable;
using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class ValidateMemberHandler(
    MemberDetailsClient memberDetailsClient)
    : IProcessStepHandler<ValidateMemberStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        ValidateMemberStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var memberDetails =
                await memberDetailsClient.GetMemberDetailsAsync(
                    processStep.MemberId,
                    processStep.MemberEnrollmentId,
                    cancellationToken);

            if (memberDetails is null)
            {
                return ProcessStepHandlerResult.Failure(
                    IntakeProcessMessages.MemberNotFound(
                        processStep.MemberId,
                        processStep.MemberEnrollmentId));
            }

            return ProcessStepHandlerResult.Success(
                requiredStep: nameof(CaptureMemberStep).Replace("Step", string.Empty));
        }
        catch (KaleidoQueryableClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                IntakeProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
