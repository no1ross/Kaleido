using Kaleido.Samples.PriorAuth.History.Process;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Process.Messages;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class StartIntakeHandler(
    IntakeDbContext dbContext,
    HistoryClient historyClient)
    : IProcessStepHandler<StartIntakeStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(
        StartIntakeStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken) =>
        HistoryRecording.InTransactionAsync(
            dbContext,
            () => RunAsync(context, cancellationToken),
            _ => ProcessStepHandlerResult.Reject(IntakeProcessMessages.HistoryNotRecorded(context.ProcessId)),
            cancellationToken);

    private async Task<ProcessStepHandlerResult> RunAsync(
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        dbContext.IntakeSessions.Add(
            new IntakeSession
            {
                IntakeSessionId = Guid.NewGuid(),
                ProcessId = context.ProcessId
            });

        await dbContext.SaveChangesAsync(cancellationToken);

        await historyClient.RecordAsync(context.ProcessId, cancellationToken);

        return ProcessStepHandlerResult.Success();
    }
}
