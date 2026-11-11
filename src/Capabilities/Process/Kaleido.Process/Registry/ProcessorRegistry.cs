using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Registry;

internal interface IProcessorRegistry
{
    IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }
}

internal sealed partial class ProcessorRegistry : IProcessorRegistry
{

    public ProcessorRegistry(
        TypeDescriber typeDescriber,
        ConstraintMapper constraintMapper,
        KaleidoServiceOptions serviceOptions,
        KaleidoProcessOptions processOptions,
        IProcessorStepRegistry stepRegistry,
        ILogger<ProcessorRegistry> logger)
    {
        ArgumentNullException.ThrowIfNull(typeDescriber);
        ArgumentNullException.ThrowIfNull(constraintMapper);
        ArgumentNullException.ThrowIfNull(serviceOptions);
        ArgumentNullException.ThrowIfNull(processOptions);
        ArgumentNullException.ThrowIfNull(stepRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        Registrations =
        [
            new ProcessorRegistryItem
            {
                IsEntryProcessor = processOptions.IsEntryProcessor,
                InitialSteps = stepRegistry.InitialRegistrations
                    .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.ToSummary())
                    .ToArray(),
                Steps = stepRegistry.Registrations
                    .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(x =>
                        x.ToRegistryItem(
                            typeDescriber,
                            constraintMapper))
                    .ToArray(),
            },
        ];

        LogRegistryBuilt(logger, serviceOptions.ServiceName, stepRegistry.Registrations.Count, stepRegistry.InitialRegistrations.Count);
    }

    public IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }

    [LoggerMessage(Level = LogLevel.Information, Message = "Process registry built for processor {ServiceName} with {StepCount} steps ({InitialCount} initial).")]
    private static partial void LogRegistryBuilt(ILogger logger, string serviceName, int stepCount, int initialCount);
}
