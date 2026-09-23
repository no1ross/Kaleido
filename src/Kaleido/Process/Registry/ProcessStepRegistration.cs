namespace Kaleido.Process.Registry;

using Kaleido.Process.Execution;

public sealed partial record ProcessStepRegistration(
    Type StepType,
    Type? StepResultType,
    Type HandlerType,
    IReadOnlyCollection<ProcessStepRegistration> Dependencies,
    IReadOnlyCollection<ProcessStepRegistration> AvailableAfter,
    IReadOnlyCollection<ProcessStepRegistration> AvailableUntil,
    RepeatableOptions Repeatable,
    ProcessStepMetadata Metadata,
    Func<Task, IProcessStepHandlerResult>? GetResultFromTask = null);

public sealed record RepeatableOptions
{
    public bool Enabled { get; init; }
}

public sealed record ProcessStepMetadata(
    string Name,
    string Description,
    string Version,
    string DisplayName);
