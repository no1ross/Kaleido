using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

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

            var session =
                await dbContext.IntakeSessions
                    .Include(x => x.Member)
                    .SingleOrDefaultAsync(
                        x => x.ProcessId == context.ProcessId,
                        cancellationToken);

            if (session is null)
            {
                session =
                    new IntakeSession
                    {
                        IntakeSessionId = Guid.NewGuid(),
                        ProcessId = context.ProcessId,
                        CreatedUtc = DateTimeOffset.UtcNow
                    };

                dbContext.IntakeSessions.Add(session);
            }

            if (session.Member is null)
            {
                session.Member =
                    new IntakeSessionMember
                    {
                        IntakeSessionId = session.IntakeSessionId
                    };
            }

            session.Member.MemberId = memberDetails.MemberId;
            session.Member.MemberEnrollmentId = memberDetails.MemberEnrollmentId;
            session.Member.MemberNumber = memberDetails.MemberNumber;
            session.Member.DisplayName = memberDetails.DisplayName;
            session.Member.DateOfService = processStep.DateOfService;

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
