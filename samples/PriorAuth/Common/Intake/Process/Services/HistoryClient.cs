using Kaleido.Http.Client;
using Kaleido.Http.Processor;
using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Services;

/// <summary>
/// Records Intake's current snapshot of a request in History, read from the intake session (call it
/// after the step saved, inside the step's transaction: it shares the step's scoped DbContext).
/// History is the source of truth for finding requests, so a recording that does not complete is an
/// error: it is logged and thrown as <see cref="HistoryNotRecordedException"/>, and the calling
/// step is rolled back and rejected.
/// </summary>
public sealed class HistoryClient(
    IntakeDbContext dbContext,
    IKaleidoProcessorClientFactory processClientFactory,
    ILogger<HistoryClient> logger)
{
    public async Task RecordAsync(
        Guid processId,
        CancellationToken cancellationToken = default)
    {
        var session =
            await dbContext.IntakeSessions
                .AsNoTracking()
                .Include(x => x.Member)
                .Include(x => x.Procedure)
                .SingleAsync(x => x.ProcessId == processId, cancellationToken);

        // Intake has no prior authorization yet: the product processor creates it on handoff.
        await UpsertAsync(
            new UpsertPriorAuthRecordStep
            {
                Status = PriorAuthorizationStatus.Draft,
                MemberNumber = session.Member?.MemberNumber ?? string.Empty,
                MemberDisplayName = session.Member?.DisplayName ?? string.Empty,
                DateOfService = session.Member?.DateOfService,
                PrimaryProcedureCode = session.Procedure?.CodeValue ?? string.Empty,
                PrimaryProcedureDescription = session.Procedure?.Description ?? string.Empty
            },
            processId,
            cancellationToken);
    }

    private async Task UpsertAsync(
        UpsertPriorAuthRecordStep step,
        Guid processId,
        CancellationToken cancellationToken)
    {
        StepExecutionResponse response;

        try
        {
            response = await processClientFactory
                .GetClient("History")
                .ExecuteStepAsync(step, cancellationToken);
        }
        catch (KaleidoHttpClientException ex)
        {
            logger.LogError(ex, "History did not record process {ProcessId}: the request failed ({StatusCode}).", processId, ex.StatusCode);

            throw new HistoryNotRecordedException($"History could not be reached: {ex.Message}", ex);
        }

        if (response.Outcome != StepExecutionOutcome.Completed)
        {
            var reason = string.Join(" ", response.BusinessMessages.Concat(response.FrameworkMessages).Select(x => x.Message));

            logger.LogError("History did not record process {ProcessId}: the upsert ended {Outcome}. {Reason}", processId, response.Outcome, reason);

            throw new HistoryNotRecordedException($"History did not record the change (outcome {response.Outcome}). {reason}".TrimEnd());
        }
    }
}
