namespace Kaleido.Process.Participant;

public interface IProcessStepEngine
{
    Task<ProcessStepResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}
