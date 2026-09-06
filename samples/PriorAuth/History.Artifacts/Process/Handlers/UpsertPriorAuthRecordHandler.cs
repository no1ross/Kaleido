using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.History.Data;
using Kaleido.Samples.PriorAuth.History.Data.Entities;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.History.Process.Handlers;

public sealed class UpsertPriorAuthRecordHandler(
    HistoryDbContext dbContext)
    : IProcessStepHandler<UpsertPriorAuthRecordStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        UpsertPriorAuthRecordStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        var existing =
            await dbContext.PriorAuthRecords
                .SingleOrDefaultAsync(
                    x => x.ProcessId == processStep.ProcessId,
                    cancellationToken);

        if (existing is null)
        {
            dbContext.PriorAuthRecords.Add(
                new PriorAuthRecord
                {
                    PriorAuthRecordId = Guid.NewGuid(),
                    ProcessId = processStep.ProcessId,
                    ProcessorName = processStep.ProcessorName,
                    Status = processStep.Status,
                    MemberNumber = processStep.MemberNumber,
                    MemberDisplayName = processStep.MemberDisplayName,
                    DateOfService = processStep.DateOfService,
                    PrimaryProcedureCode = processStep.PrimaryProcedureCode,
                    PrimaryProcedureDescription = processStep.PrimaryProcedureDescription,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    LastUpdatedUtc = DateTimeOffset.UtcNow
                });
        }
        else
        {
            existing.ProcessorName = processStep.ProcessorName;
            existing.Status = processStep.Status;
            existing.MemberNumber = processStep.MemberNumber;
            existing.MemberDisplayName = processStep.MemberDisplayName;
            existing.DateOfService = processStep.DateOfService;
            existing.PrimaryProcedureCode = processStep.PrimaryProcedureCode;
            existing.PrimaryProcedureDescription = processStep.PrimaryProcedureDescription;
            existing.LastUpdatedUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ProcessStepHandlerResult.Success();
    }
}
