using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Registry;

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
