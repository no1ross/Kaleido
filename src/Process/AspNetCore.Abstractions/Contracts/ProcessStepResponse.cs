using Kaleido.Process.Registry;

namespace Kaleido.Process.AspNetCore.Contracts;

public static class ProcessorRegistryResponseFactory
{
    public static ProcessorRegistryResponse FromRegistration(
        ProcessorRegistryItem registration,
        KaleidoServiceOptions serviceOptions)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(serviceOptions);

        var serviceName = serviceOptions.ServiceName;

        return new ProcessorRegistryResponse
        {
            ServiceName = serviceName,
            Name = serviceName,
            Description = serviceOptions.Description,
            DisplayName = serviceOptions.DisplayName,
            IsEntryProcessor = registration.IsEntryProcessor,
            RegistryUrl = ProcessContractUrls.Registry(serviceName),
            InitialSteps = registration.InitialSteps
                .Select(x => ProcessStepResponseFactory.ToSummary(x, serviceName))
                .ToArray(),
            Steps = registration.Steps
                .Select(x => ProcessStepResponseFactory.FromRegistration(x, serviceName))
                .ToArray()
        };
    }
}

public static class ProcessorCatalogResponseFactory
{
    public static ProcessorCatalogResponse FromRegistration(
        ProcessorRegistryItem registration,
        KaleidoServiceOptions serviceOptions)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(serviceOptions);

        var serviceName = serviceOptions.ServiceName;

        return new ProcessorCatalogResponse
        {
            ServiceName = serviceName,
            Name = serviceName,
            Description = serviceOptions.Description,
            DisplayName = serviceOptions.DisplayName,
            IsEntryProcessor = registration.IsEntryProcessor,
            RegistryUrl = ProcessContractUrls.Registry(serviceName),
            InitialSteps = registration.InitialSteps
                .Select(x => ProcessStepResponseFactory.ToSummary(x, serviceName))
                .ToArray()
        };
    }
}

public static class ProcessStepResponseFactory
{
    public static ProcessStepResponse FromRegistration(
        ProcessorStepRegistryItem registration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var stepName =
            registration.Name.ToLowerInvariant();

        return new ProcessStepResponse
        {
            Name = registration.Name,
            Description = registration.Description,
            DisplayName = registration.DisplayName,
            Version = registration.Version,
            Repeatable = registration.Repeatable,
            Fields = registration.Fields
                .Select(ProcessFieldMetadata.FromRegistryItem)
                .ToArray(),
            Dependencies = registration.Dependencies
                .OrderBy(x => x.Name)
                .Select(x => ToSummary(x, serviceName))
                .ToArray(),
            AvailableAfter = registration.AvailableAfter
                .OrderBy(x => x.Name)
                .Select(x => ToSummary(x, serviceName))
                .ToArray(),
            AvailableUntil = registration.AvailableUntil
                .OrderBy(x => x.Name)
                .Select(x => ToSummary(x, serviceName))
                .ToArray(),
            Result = registration.Result is null
                ? null
                : ProcessStepResultMetadata.FromRegistryItem(registration.Result),
            ExecuteUrl = ProcessContractUrls.ExecuteStep(serviceName, stepName),
            MetadataUrl = ProcessContractUrls.StepMetadata(serviceName, stepName)
        };
    }

    internal static ProcessStepSummary ToSummary(
        ProcessorStepSummary registration,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var stepName =
            registration.Name.ToLowerInvariant();

        return new ProcessStepSummary
        {
            Name = registration.Name,
            Description = registration.Description,
            DisplayName = registration.DisplayName,
            Version = registration.Version,
            Repeatable = registration.Repeatable,
            ExecuteUrl = ProcessContractUrls.ExecuteStep(serviceName, stepName),
            MetadataUrl = ProcessContractUrls.StepMetadata(serviceName, stepName)
        };
    }
}

public sealed record ProcessorRegistryResponse : ProcessorRegistryItem
{
    /// <summary>
    /// The service name — matches <see cref="KaleidoServiceOptions.ServiceName"/>.
    /// Allows consumers to identify which service this processor belongs to.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

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
