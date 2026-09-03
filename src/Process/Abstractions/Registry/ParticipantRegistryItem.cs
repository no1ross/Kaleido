namespace Kaleido.Process.Registry;

public record ProcessorRegistryItem
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public IReadOnlyCollection<ProcessorStepSummary> InitialSteps { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepRegistryItem> Steps { get; init; }
        = [];
}

public record ProcessorStepRegistryItem
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    public IReadOnlyCollection<ProcessorInputFieldDescriptor> Fields { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepSummary> Dependencies { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepSummary> AvailableAfter { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepSummary> AvailableUntil { get; init; }
        = [];

    public ProcessorStepResultDescriptor? Result { get; init; }
}

public record ProcessorStepSummary
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }
}

public record ProcessorPropertyDescriptor
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DataTypeDescriptor DataType { get; init; }
}

public record ProcessorInputFieldDescriptor : ProcessorPropertyDescriptor
{
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];
}

public record ProcessorOutputFieldDescriptor : ProcessorPropertyDescriptor;

public record ProcessorStepResultDescriptor
{
    public IReadOnlyCollection<ProcessorOutputFieldDescriptor> OutputFields { get; init; }
        = [];
}
