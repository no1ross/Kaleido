using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.History.Process.Steps;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class StartIntakeHandler(
    IntakeDbContext dbContext,
    HistoryClient historyClient)
    : IProcessStepHandler<StartIntakeStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        StartIntakeStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        dbContext.IntakeSessions.Add(
            new IntakeSession
            {
                IntakeSessionId = Guid.NewGuid(),
                ProcessId = context.ProcessId,
                CreatedUtc = DateTimeOffset.UtcNow
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        await historyClient.UpsertAsync(
            new UpsertPriorAuthRecordStep
            {
                ProcessorName = "intake",
                Status = PriorAuthorizationStatus.Draft
            },
            context.ProcessId,
            cancellationToken);

        return ProcessStepHandlerResult.Success();
    }
}
