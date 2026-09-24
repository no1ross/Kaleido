namespace Kaleido.Http.Process.Contracts;

[ExcludeFromCodeCoverage]
public sealed record ProcessorRegistryResponse
{
    /// <summary>
    /// The service name — matches <see cref="KaleidoServiceOptions.ServiceName"/>.
    /// Allows consumers to identify which service this processor belongs to.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public bool IsEntryProcessor { get; init; }

    public string RegistryUrl { get; init; }
        = string.Empty;

    public IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepResponse> Steps { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record ProcessCatalogResponse
{
    public IReadOnlyCollection<ProcessorCatalogResponse> Processors
    {
        get;
        init;
    }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record ProcessorCatalogResponse
{
    /// <summary>
    /// The service name — matches <see cref="KaleidoServiceOptions.ServiceName"/>.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public bool IsEntryProcessor { get; init; }

    public string RegistryUrl { get; init; }
        = string.Empty;

    public IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;

    public IReadOnlyCollection<ProcessFieldMetadata> Fields { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepSummary> Dependencies { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepSummary> AvailableAfter { get; init; }
        = [];

    public IReadOnlyCollection<ProcessStepSummary> AvailableUntil { get; init; }
        = [];

    public ProcessStepResultMetadata? Result { get; init; }
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepSummary
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;
}

[ExcludeFromCodeCoverage]
public sealed record ProcessFieldMetadata : ProcessorInputFieldDescriptor;

[ExcludeFromCodeCoverage]
public sealed record ProcessOutputFieldMetadata : ProcessorOutputFieldDescriptor;

[ExcludeFromCodeCoverage]
public sealed record ProcessStepResultMetadata
{
    public IReadOnlyCollection<ProcessOutputFieldMetadata> OutputFields { get; init; }
        = [];
}
