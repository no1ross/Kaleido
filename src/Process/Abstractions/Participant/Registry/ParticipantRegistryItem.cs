namespace Kaleido.Process.Participant.Registry;

public record ParticipantRegistryItem
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public IReadOnlyCollection<ParticipantStepSummary> InitialSteps { get; init; }
        = [];

    public IReadOnlyCollection<ParticipantStepRegistryItem> Steps { get; init; }
        = [];
}

public record ParticipantStepRegistryItem
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    public IReadOnlyCollection<ParticipantInputFieldDescriptor> Fields { get; init; }
        = [];

    public IReadOnlyCollection<ParticipantStepSummary> Dependencies { get; init; }
        = [];

    public IReadOnlyCollection<ParticipantStepSummary> AvailableAfter { get; init; }
        = [];

    public IReadOnlyCollection<ParticipantStepSummary> AvailableUntil { get; init; }
        = [];

    public ParticipantStepResultDescriptor? Result { get; init; }
}

public record ParticipantStepSummary
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }
}

public record ParticipantPropertyDescriptor
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DataTypeDescriptor DataType { get; init; }
}

public record ParticipantInputFieldDescriptor : ParticipantPropertyDescriptor
{
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];
}

public record ParticipantOutputFieldDescriptor : ParticipantPropertyDescriptor;

public record ParticipantStepResultDescriptor
{
    public IReadOnlyCollection<ParticipantOutputFieldDescriptor> OutputFields { get; init; }
        = [];
}
