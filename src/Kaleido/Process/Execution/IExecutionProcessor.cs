using Kaleido.Process.Context;
using Kaleido.Process.Planning;

namespace Kaleido.Process.Execution
{
    public interface IExecutionProcessor
    {
        Task<ProcessExecutionResult> ExecuteAsync(
            IReadOnlyCollection<StepCandidate> candidates,
            ProcessorContext context,
            ProcessorRequest originalRequest,
            CancellationToken cancellationToken = default);
    }
}