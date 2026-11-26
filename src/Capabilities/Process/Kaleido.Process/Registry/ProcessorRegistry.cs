using Kaleido.Registry;
using Microsoft.Extensions.Logging;

namespace Kaleido.Process.Registry;

internal interface IProcessorRegistry
{
    IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }

    IReadOnlyCollection<ProcessRegistryItem> Published { get; }
}

internal sealed partial class ProcessorRegistry : IProcessorRegistry, IKaleidoLocalCapabilities
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
        Published = [.. Registrations.Select(item => ToPublished(item, serviceOptions))];

        LogRegistryBuilt(logger, serviceOptions.ServiceName, stepRegistry.Registrations.Count, stepRegistry.InitialRegistrations.Count);
    }

    public IReadOnlyCollection<ProcessorRegistryItem> Registrations { get; }

    public IReadOnlyCollection<ProcessRegistryItem> Published { get; }

    public KaleidoRegistry Capabilities() => new() { Processes = Published };

    private static ProcessRegistryItem ToPublished(ProcessorRegistryItem processor, KaleidoServiceOptions service) => new()
    {
        ServiceName = service.ServiceName,
        ServiceVersion = service.ServiceVersion,
        Name = service.ServiceName,
        Description = service.Description,
        DisplayName = service.DisplayName,
        IsEntryProcessor = processor.IsEntryProcessor,
        InitialSteps = [.. processor.InitialSteps.Select(ToPublished)],
        Steps = [.. processor.Steps.Select(ToPublished)],
    };

    private static ProcessStepResponse ToPublished(ProcessorStepRegistryItem step) => new()
    {
        Name = step.Name,
        Description = step.Description,
        DisplayName = step.DisplayName,
        Repeatable = step.Repeatable,
        IsInformationStep = step.IsInformationStep,
        Destructive = step.Destructive,
        External = step.External,
        RequiresConfirmation = step.RequiresConfirmation,
        Authorization = step.Authorization,
        IsInitial = step.Dependencies.Count == 0 && step.AvailableAfter.Count == 0,
        Fields = [.. step.Fields.Select(ToPublished)],
        Dependencies = [.. step.Dependencies.Select(ToPublished)],
        AvailableAfter = [.. step.AvailableAfter.Select(ToPublished)],
        AvailableUntil = [.. step.AvailableUntil.Select(ToPublished)],
        Result = step.Result is { } result ? new ProcessStepResultMetadata
        {
            OutputFields = [.. result.OutputFields.Select(field => new ProcessOutputFieldMetadata
            {
                Name = field.Name,
                Description = field.Description,
                DataType = ToPublished(field.DataType),
                Classifications = field.Classifications,
            })],
        } : null,
    };

    private static ProcessStepSummary ToPublished(ProcessorStepSummary summary) => new()
    {
        Name = summary.Name,
        Description = summary.Description,
        DisplayName = summary.DisplayName,
        Repeatable = summary.Repeatable,
        IsInformationStep = summary.IsInformationStep,
        Destructive = summary.Destructive,
        External = summary.External,
        RequiresConfirmation = summary.RequiresConfirmation,
        Authorization = summary.Authorization,
    };

    private static ProcessFieldMetadata ToPublished(ProcessorInputFieldDescriptor field) => new()
    {
        Name = field.Name,
        Description = field.Description,
        DisplayName = field.DisplayName,
        Prompt = field.Prompt,
        DataType = ToPublished(field.DataType),
        Classifications = field.Classifications,
        Lookup = field.Lookup is { } lookup ? new ProcessFieldLookup
        {
            Service = lookup.Service,
            Source = lookup.Source,
            View = lookup.View,
            Field = lookup.Field,
            Group = lookup.Group,
            Display = lookup.Display,
        } : null,
        Constraints = [.. field.Constraints.Select(constraint => new ConstraintMetadata
        {
            Type = constraint.Type,
            Parameters = [.. constraint.Parameters.Select(value => new ConstraintParameterMetadata { Name = value.Name, Value = value.Value })],
        })],
    };

    private static DataTypeMetadata ToPublished(DataTypeDescriptor descriptor) => new(
        descriptor.Type,
        descriptor.Format,
        descriptor.Nullable,
        descriptor.EnumValues?.Select(value => new EnumValueMetadata(value.Value, value.Name, value.Description)).ToArray(),
        descriptor.ItemType is { } item ? ToPublished(item) : null);

    [LoggerMessage(Level = LogLevel.Information, Message = "Process registry built for processor {ServiceName} with {StepCount} steps ({InitialCount} initial).")]
    private static partial void LogRegistryBuilt(ILogger logger, string serviceName, int stepCount, int initialCount);
}
