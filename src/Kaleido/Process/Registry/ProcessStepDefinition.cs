namespace Kaleido.Process.Registry;

internal sealed record ProcessStepDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    public ICollection<ProcessStepDefinition> Dependencies { get; } =
        [];

    public ICollection<ProcessStepDefinition> AvailableAfter { get; } =
        [];

    public ICollection<ProcessStepDefinition> AvailableUntil { get; } =
        [];
}

internal sealed record ProcessStepTypeDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    public ICollection<Type> Dependencies { get; } =
        [];

    public ICollection<Type> AvailableAfter { get; } =
        [];

    public ICollection<Type> AvailableUntil { get; } =
        [];
}