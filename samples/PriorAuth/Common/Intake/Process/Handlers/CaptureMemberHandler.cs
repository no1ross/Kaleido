using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
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
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureMemberStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
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
                return ProcessStepHandlerResult.Failure(
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

            await historyClient.UpsertAsync(
                new UpsertPriorAuthRecordStep
                {
                    ProcessorName = "intake",
                    Status = PriorAuthorizationStatus.Draft,
                    MemberNumber = session.Member!.MemberNumber,
                    MemberDisplayName = session.Member.DisplayName,
                    DateOfService = session.Member.DateOfService
                },
                context.ProcessId,
                cancellationToken);

            return ProcessStepHandlerResult.Success(
                requiredStep: nameof(CaptureRequestedServiceStep).Replace("Step", string.Empty));
        }
        catch (KaleidoHttpClientException ex)
        {
            return ProcessStepHandlerResult.Failure(
                IntakeProcessMessages.QueryableRequestFailed(
                    ex.Errors.FirstOrDefault()?.Code ?? "QUERYABLE_REQUEST_FAILED",
                    ex.Message));
        }
    }
}
