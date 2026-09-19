namespace Kaleido.Process.Registry;

internal sealed class ProcessStepDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    public ICollection<ProcessStepDefinition> Dependencies { get; } =
        new List<ProcessStepDefinition>();

    public ICollection<ProcessStepDefinition> AvailableAfter { get; } =
        new List<ProcessStepDefinition>();

    public ICollection<ProcessStepDefinition> AvailableUntil { get; } =
        new List<ProcessStepDefinition>();
}

internal sealed class ProcessStepTypeDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    public ICollection<Type> Dependencies { get; } =
        new List<Type>();

    public ICollection<Type> AvailableAfter { get; } =
        new List<Type>();

    public ICollection<Type> AvailableUntil { get; } =
        new List<Type>();
}