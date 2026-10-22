using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class CaptureMemberHandler(
    RadiologyDbContext dbContext,
    IMemberEligibilityService memberEligibilityService,
    HistoryClient historyClient)
    : IProcessStepHandler<CaptureMemberStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureMemberStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        HistoryRecording.InTransactionAsync(
            dbContext,
            () => RunAsync(processStep, context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(RadiologyProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult> RunAsync(
        CaptureMemberStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Full eligibility validation — also checks PriorAuth exists
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

            var memberDetails = eligibility.MemberDetails!;

            // Load the existing PriorAuthorization — must exist (created by StartRadiologyIntake)
            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .Include(x => x.Member)
                    .SingleAsync(
                        x => x.ProcessId == context.ProcessId,
                        cancellationToken);

            if (priorAuthorization.Member is null)
            {
                priorAuthorization.Member =
                    new PriorAuthorizationMember
                    {
                        PriorAuthorizationId = priorAuthorization.PriorAuthorizationId
                    };
            }

            priorAuthorization.Member.MemberId = memberDetails.MemberId;
            priorAuthorization.Member.MemberEnrollmentId = memberDetails.MemberEnrollmentId;
            priorAuthorization.Member.MemberNumber = memberDetails.MemberNumber;
            priorAuthorization.Member.DisplayName = memberDetails.DisplayName;
            priorAuthorization.Member.PlanId = memberDetails.PlanId;
            priorAuthorization.Member.PlanName = memberDetails.PlanName;
            priorAuthorization.Member.LineOfBusiness = memberDetails.LineOfBusiness;
            priorAuthorization.DateOfService = processStep.DateOfService;

            await dbContext.SaveChangesAsync(cancellationToken);

            await historyClient.RecordAsync(context.ProcessId, cancellationToken);

            // Route to the correct next step based on the already-captured requested service
            var routing =
                await memberEligibilityService.RouteByModalityAsync(
                    context.ProcessId,
                    memberDetails,
                    cancellationToken);

            if (!routing.Succeeded)
            {
                return ProcessStepHandlerResult.Reject(routing.FailureMessage!);
            }

            return routing.Route == ModalityRoute.CaptureMriInfo
                ? ProcessStepHandlerResult.RequireInformation<CaptureMriInfoStep>(routing.InformationRequest!)
                : ProcessStepHandlerResult.RequireInformation<ConfirmCtInsteadOfMriStep>(routing.InformationRequest!);
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Reject(
                RadiologyProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
