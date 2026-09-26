namespace Kaleido.Process.Registry;

[ExcludeFromCodeCoverage]
public sealed partial record ProcessStepRegistration(
    Type StepType,
    Type? StepResultType,
    Type HandlerType,
    IReadOnlyCollection<ProcessStepRegistration> Dependencies,
    IReadOnlyCollection<ProcessStepRegistration> AvailableAfter,
    IReadOnlyCollection<ProcessStepRegistration> AvailableUntil,
    RepeatableOptions Repeatable,
    ProcessStepMetadata Metadata,
    Func<Task, IProcessStepHandlerResult>? GetResultFromTask = null,
    Func<object, object, ProcessStepContext, CancellationToken, Task>? InvokeHandlerAsync = null);

[ExcludeFromCodeCoverage]
public sealed record RepeatableOptions
{
    public bool Enabled { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepMetadata(
    string Name,
    string Description,
    string Version,
    string DisplayName);
