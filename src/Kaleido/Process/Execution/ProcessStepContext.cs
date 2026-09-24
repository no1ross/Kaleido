using Kaleido.Process.Context;

namespace Kaleido.Process.Execution;

[ExcludeFromCodeCoverage]
public sealed record ProcessStepContext
(
    Guid ProcessId,
    StepContext StepContext,
    IReadOnlyCollection<string> AvailableNextSteps,
    ProcessorRequest OriginalRequest
);
