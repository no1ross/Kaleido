using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Registry;

[ExcludeFromCodeCoverage]
public record ProcessorRegistryItem
{
    /// <summary>
    /// Marks this processor as the entry point for the application workflow.
    /// When true, consumers should start with this processor.
    /// Only one processor in a distributed system should have this set to true.
    /// </summary>
    public bool IsEntryProcessor { get; init; }

    public IReadOnlyCollection<ProcessorStepSummary> InitialSteps { get; init; }
        = [];

    public IReadOnlyCollection<ProcessorStepRegistryItem> Steps { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
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

[ExcludeFromCodeCoverage]
public record ProcessorStepSummary
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }
}

[ExcludeFromCodeCoverage]
public record ProcessorPropertyDescriptor
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required DataTypeDescriptor DataType { get; init; }
}

[ExcludeFromCodeCoverage]
public record ProcessorInputFieldDescriptor : ProcessorPropertyDescriptor
{
    public IReadOnlyCollection<ConstraintContract> Constraints { get; init; }
        = [];
}

[ExcludeFromCodeCoverage]
public record ProcessorOutputFieldDescriptor : ProcessorPropertyDescriptor;

[ExcludeFromCodeCoverage]
public record ProcessorStepResultDescriptor
{
    public IReadOnlyCollection<ProcessorOutputFieldDescriptor> OutputFields { get; init; }
        = [];
}

public interface IProcessRegistry
{
    IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }
}

internal sealed class ProcessRegistry : IProcessRegistry
{
    private readonly IReadOnlyCollection<ProcessorRegistryItem> _registrations;

    public ProcessRegistry(
        IDataTypeMapper dataTypeMapper,
        IConstraintMapper constraintMapper,
        KaleidoServiceOptions serviceOptions,
        IProcessStepRegistry stepRegistry,
        ILogger<ProcessRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(dataTypeMapper);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(serviceOptions);
        ArgumentNullException.ThrowIfNull(stepRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _registrations =
        [
            new ProcessorRegistryItem
            {
                IsEntryProcessor = serviceOptions.IsEntryProcessor,
                InitialSteps = stepRegistry.InitialRegistrations
                    .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.ToSummary())
                    .ToArray(),
                Steps = stepRegistry.Registrations
                    .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x =>
                        x.ToRegistryItem(
                            dataTypeMapper,
                            constraintMapper))
                    .ToArray()
            }
        ];

        logger.LogInformation(
            "Process registry built for processor {ServiceName} with {StepCount} steps ({InitialCount} initial).",
            serviceOptions.ServiceName,
            stepRegistry.Registrations.Count,
            stepRegistry.InitialRegistrations.Count);
    }

    public IReadOnlyCollection<ProcessorRegistryItem> Registrations =>
        _registrations;
}
