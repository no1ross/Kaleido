using Kaleido.Process.Execution;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Kaleido.Samples.PriorAuth.Intake.Process.Steps;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Handlers;

public sealed class StartIntakeHandler(
    IntakeDbContext dbContext)
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

        return ProcessStepHandlerResult.Success();
    }
}
