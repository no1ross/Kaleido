using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.Participant.Execution;

public interface IProcessStepInvoker
{
    Task<ProcessStepHandlerResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);
}
