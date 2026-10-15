using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class CaptureMemberHandler(
    IntakeDbContext dbContext,
    MemberDetailsClient memberDetailsClient,
    HistoryClient historyClient)
    : IProcessStepHandler<CaptureMemberStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureMemberStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default) =>
        HistoryRecording.InTransactionAsync(
            dbContext,
            () => RunAsync(processStep, context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(IntakeProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult> RunAsync(
        CaptureMemberStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            // Intake can only verify the member exists — eligibility is determined by Radiology
            var memberDetails =
                await memberDetailsClient.GetMemberDetailsAsync(
                    processStep.MemberId,
                    processStep.MemberEnrollmentId,
                    cancellationToken);

            if (memberDetails is null)
            {
                return ProcessStepHandlerResult.Reject(
                    IntakeProcessMessages.MemberNotFound(
                        processStep.MemberId,
                        processStep.MemberEnrollmentId));
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

            await historyClient.RecordAsync(context.ProcessId, cancellationToken);

            return ProcessStepHandlerResult.Success<CaptureRequestedServiceStep>();
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Reject(
                IntakeProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
