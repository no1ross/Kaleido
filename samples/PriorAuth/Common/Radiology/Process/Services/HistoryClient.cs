using Kaleido.Http.Client;
using Kaleido.Http.Processor;
using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Services;

/// <summary>
/// Records Radiology's current snapshot of a request in History, read from its prior authorization
/// (call it after the step saved, inside the step's transaction: it shares the step's scoped
/// DbContext). History is the source of truth for finding requests, so a recording that does not
/// complete is an error: it is logged and thrown as <see cref="HistoryNotRecordedException"/>, and
/// the calling step is rolled back and rejected.
/// </summary>
public sealed class HistoryClient(
    RadiologyDbContext dbContext,
    IKaleidoProcessorClientFactory processClientFactory,
    ILogger<HistoryClient> logger)
{
    public async Task RecordAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        var priorAuthorization =
            await dbContext.PriorAuthorizations
                .AsNoTracking()
                .Include(x => x.Member)
                .Include(x => x.RequestedServices)
                .SingleAsync(x => x.ProcessId == processId, cancellationToken);

        var primaryService = priorAuthorization.RequestedServices.MinBy(x => x.CreatedUtc);

        await UpsertAsync(
            new UpsertPriorAuthRecordStep
            {
                PriorAuthorizationId = priorAuthorization.PriorAuthorizationId,
                Status = priorAuthorization.Status,
                MemberNumber = priorAuthorization.Member?.MemberNumber ?? string.Empty,
                MemberDisplayName = priorAuthorization.Member?.DisplayName ?? string.Empty,
                DateOfService = priorAuthorization.DateOfService,
                PrimaryProcedureCode = primaryService?.ResolvedCodeValue ?? string.Empty,
                PrimaryProcedureDescription = primaryService?.Description ?? string.Empty
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
