using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.Participant.Execution;

public interface IProcessStepInvoker
{
    Task<ProcessStepResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        StepContext context,
        CancellationToken cancellationToken = default);
}
