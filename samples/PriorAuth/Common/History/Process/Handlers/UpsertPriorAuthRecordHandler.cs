using Kaleido.Observability;
using Kaleido.Samples.PriorAuth.History.Data;
using Kaleido.Samples.PriorAuth.History.Data.Entities;
using Kaleido.Samples.PriorAuth.History.Process.Steps;

namespace Kaleido.Samples.PriorAuth.History.Process.Handlers;

/// <summary>
/// Keeps the latest snapshot of a request: one record per process, overwritten by each recording.
/// The recording processor is the calling processor Kaleido stamped on the call (trusted for the
/// authenticated service call this step requires).
/// </summary>
public sealed class UpsertPriorAuthRecordHandler(
    HistoryDbContext dbContext,
    IKaleidoCorrelationContextAccessor correlation)
    : IProcessStepHandler<UpsertPriorAuthRecordStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        UpsertPriorAuthRecordStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        if (correlation.Current.CallingProcessorName is not { Length: > 0 } processorName)
        {
            return ProcessStepHandlerResult.Reject(
                new ProcessMessage
                {
                    Code = "CALLING_PROCESSOR_REQUIRED",
                    Type = MessageType.Error,
                    Message = "History records a request only for the processor step that changed it: call this step from inside a processor step."
                });
        }

        var record = await dbContext.PriorAuthRecords.FindAsync([context.ProcessId], cancellationToken);

        if (record is null)
        {
            record = new PriorAuthRecord
            {
                ProcessId = context.ProcessId,
                CreatedUtc = DateTimeOffset.UtcNow
            };

            dbContext.PriorAuthRecords.Add(record);
        }

        record.ProcessorName = processorName;
        record.PriorAuthorizationId = processStep.PriorAuthorizationId;
        record.Status = processStep.Status;
        record.MemberNumber = processStep.MemberNumber;
        record.MemberDisplayName = processStep.MemberDisplayName;
        record.DateOfService = processStep.DateOfService;
        record.PrimaryProcedureCode = processStep.PrimaryProcedureCode;
        record.PrimaryProcedureDescription = processStep.PrimaryProcedureDescription;
        record.LastUpdatedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ProcessStepHandlerResult.Success();
    }
}
