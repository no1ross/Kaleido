using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.Participant.Execution;

public interface IProcessStepInvoker
{
    Task<ProcessStepInvokerResult> ExecuteAsync(
        ProcessStepRegistration registration,
        object processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default);

}

public sealed record ProcessStepInvokerResult
{
    public bool Succeeded { get; init; }

    public string? RequiredStep { get; init; }

    public object Response { get; init; } = null!;

    public IReadOnlyCollection<ProcessMessage> Messages { get; init; }
        = [];
}

