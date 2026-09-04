using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Kaleido.Samples.PriorAuth.Radiology;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Process.Messages;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Handlers;

public sealed class CaptureMemberHandler(
    IntakeDbContext dbContext,
    MemberDetailsClient memberDetailsClient)
    : IProcessStepHandler<CaptureMemberStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureMemberStep processStep,
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

            if (processStep.DateOfService < memberDetails.EffectiveDate)
            {
                return ProcessStepHandlerResult.Failure(
                    IntakeProcessMessages.CoverageNotYetEffective(
                        processStep.MemberEnrollmentId,
                        processStep.DateOfService,
                        memberDetails.EffectiveDate));
            }

            if (memberDetails.TerminationDate is DateOnly terminationDate
                && processStep.DateOfService > terminationDate)
            {
                return ProcessStepHandlerResult.Failure(
                    IntakeProcessMessages.CoverageTerminated(
                        processStep.MemberEnrollmentId,
                        processStep.DateOfService,
                        terminationDate));
            }

            var priorAuthorization =
                await dbContext.PriorAuthorizations
                    .Include(x => x.Member)
                    .SingleOrDefaultAsync(
                        x => x.ProcessId == context.ProcessId,
                        cancellationToken);

            if (priorAuthorization is null)
            {
                priorAuthorization =
                    new PriorAuthorization
                    {
                        PriorAuthorizationId = Guid.NewGuid(),
                        ProcessId = context.ProcessId,
                        Status = PriorAuthorizationStatus.Draft,
                        CreatedUtc = DateTimeOffset.UtcNow
                    };

                dbContext.PriorAuthorizations.Add(priorAuthorization);
            }

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

            await dbContext.SaveChangesAsync(cancellationToken);

            return ProcessStepHandlerResult.Success();
        }
        catch (QueryableClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                IntakeProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
