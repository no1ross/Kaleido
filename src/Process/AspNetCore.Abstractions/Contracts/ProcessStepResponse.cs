using Kaleido.Process.Registry;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessorRegistryResponse : ProcessorRegistryItem
{
    public string RegistryUrl { get; init; }
        = string.Empty;

    public new IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; }
        = [];

    public new IReadOnlyCollection<ProcessStepResponse> Steps { get; init; }
        = [];
}

public sealed record ProcessCatalogResponse
{
    public IReadOnlyCollection<ProcessorCatalogResponse> Processors
    {
        get;
        init;
    }
        = [];
}

public sealed record ProcessorCatalogResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public string RegistryUrl { get; init; }
        = string.Empty;

    public IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; }
        = [];
}

public sealed record ProcessStepResponse : ProcessorStepRegistryItem
{
    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;

    public new IReadOnlyCollection<ProcessFieldMetadata> Fields { get; init; }
        = [];

    public new IReadOnlyCollection<ProcessStepSummary> Dependencies { get; init; }
        = [];

    public new IReadOnlyCollection<ProcessStepSummary> AvailableAfter { get; init; }
        = [];

    public new IReadOnlyCollection<ProcessStepSummary> AvailableUntil { get; init; }
        = [];

    public new ProcessStepResultMetadata? Result { get; init; }
}

public sealed record ProcessStepSummary : ProcessorStepSummary
{
    /// <summary>
    /// The name of the processor that owns this step.
    /// Null for steps belonging to the local processor.
    /// </summary>
    public string? ProcessorName { get; init; }

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;
}

public sealed record ProcessFieldMetadata : ProcessorInputFieldDescriptor
{
    public static ProcessFieldMetadata FromRegistryItem(
        ProcessorInputFieldDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ProcessFieldMetadata
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            Constraints = item.Constraints
        };
    }
}

public sealed record ProcessOutputFieldMetadata : ProcessorOutputFieldDescriptor
{
    public static ProcessOutputFieldMetadata FromRegistryItem(
        ProcessorOutputFieldDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ProcessOutputFieldMetadata
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType
        };
    }
}

public sealed record ProcessStepResultMetadata : ProcessorStepResultDescriptor
{
    public new IReadOnlyCollection<ProcessOutputFieldMetadata> OutputFields { get; init; }
        = [];

    public static ProcessStepResultMetadata FromRegistryItem(
        ProcessorStepResultDescriptor item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ProcessStepResultMetadata
        {
            OutputFields = item.OutputFields
                .Select(ProcessOutputFieldMetadata.FromRegistryItem)
                .ToArray()
        };
    }
}

