namespace Kaleido.Process.Registry;

public sealed record ProcessStepRegistration(
    Type StepType,
    Type? StepResultType,
    Type HandlerType,
    IReadOnlyCollection<ProcessStepRegistration> Dependencies,
    IReadOnlyCollection<ProcessStepRegistration> AvailableAfter,
    IReadOnlyCollection<ProcessStepRegistration> AvailableUntil,
    RepeatableOptions Repeatable,
    ProcessStepMetadata Metadata);


public sealed record RepeatableOptions
{
    public bool Enabled { get; init; }
}

public sealed record ProcessStepMetadata(
    string Name,
    string Description,
    string Version,
    string DisplayName);
