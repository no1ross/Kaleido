namespace Kaleido.Process.Participant;

public interface IProcessStepEngine
{
    Task<ProcessStepResult> ExecuteAsync<TProcessStep>(
        TProcessStep processStep,
        CancellationToken cancellationToken = default);
}