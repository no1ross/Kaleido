using Kaleido.Process.Context;
using Kaleido.Process.Registry;

namespace Kaleido.Process.Execution;

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

