using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Planning;

namespace Kaleido.Process.Participant.Execution;

public interface IProcessExecutor
{
    Task<ProcessExecutionResult> ExecuteAsync(
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context,
        CancellationToken cancellationToken = default);
}
