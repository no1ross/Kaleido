using Kaleido.Processor.Context;

namespace Kaleido.Processor.Execution;

[ExcludeFromCodeCoverage]
public sealed record ProcessStepContext
(
    Guid ProcessId,
    StepContext StepContext,
    IReadOnlyCollection<string> AvailableNextSteps,
    ProcessorRequest OriginalRequest
);
