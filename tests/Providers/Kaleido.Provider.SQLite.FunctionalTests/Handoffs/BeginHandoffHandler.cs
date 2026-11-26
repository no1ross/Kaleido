namespace Kaleido.Provider.SQLite.FunctionalTests.Handoffs;

public sealed class BeginHandoffHandler : IProcessStepHandler<BeginHandoff>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(BeginHandoff step, ProcessStepContext context, CancellationToken cancellationToken)
    {
        var target = new ProcessResult
        {
            ProcessId = context.ProcessId,
            ServiceVersion = "2",
            State = ProcessExecutionState.Active,
            Steps = [new ProcessStepResult
            {
                StepName = "ReviewOrderStep",
                CandidateStatus = StepCandidateStatus.Built,
                IncludedInExecutionPlan = true,
                ExecutionStatus = StepExecutionStatus.Completed,
            }],
        };
        return Task.FromResult(ProcessStepHandlerResult.HandOff("catalog", target, "ReviewOrderStep"));
    }
}
