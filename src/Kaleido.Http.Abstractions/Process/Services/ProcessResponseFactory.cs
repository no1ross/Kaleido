using Kaleido.Http.Process.Contracts;

namespace Kaleido.Http.Process.Services;

/// <summary>
/// Projects process runtime registry types into HTTP wire contract types.
/// Injectable so endpoint and service tests can mock the projection boundary.
/// </summary>
public interface IProcessResponseFactory
{
    ProcessorRegistryResponse CreateRegistryResponse(
        ProcessorRegistryItem registration,
        KaleidoServiceOptions serviceOptions);

    ProcessorCatalogResponse CreateCatalogResponse(
        ProcessorRegistryItem registration,
        KaleidoServiceOptions serviceOptions);

    ProcessStepResponse CreateStepResponse(
        ProcessorStepRegistryItem registration,
        string serviceName);

    ProcessStepSummary CreateStepSummary(
        ProcessorStepSummary registration,
        string serviceName);
}

public sealed class ProcessResponseFactory : IProcessResponseFactory
{
    public ProcessorRegistryResponse CreateRegistryResponse(
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
                .Select(x => new ProcessStepSummary
                {
                    Name = x.Name,
                    Description = x.Description,
                    DisplayName = x.DisplayName,
                    Version = x.Version,
                    Repeatable = x.Repeatable,
                    ExecuteUrl = string.Empty,
                    MetadataUrl = string.Empty
                })
                .ToArray(),
            Steps = registration.Steps
                .Select(x => CreateStepResponse(x, serviceName))
                .ToArray()
        };
    }

    public ProcessorCatalogResponse CreateCatalogResponse(
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
                .Select(x => CreateStepSummary(x, serviceName))
                .ToArray()
        };
    }

    public ProcessStepResponse CreateStepResponse(
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
                .Select(CreateFieldMetadata)
                .ToArray(),
            Dependencies = registration.Dependencies
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => CreateStepSummary(x, serviceName))
                .ToArray(),
            AvailableAfter = registration.AvailableAfter
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => CreateStepSummary(x, serviceName))
                .ToArray(),
            AvailableUntil = registration.AvailableUntil
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => CreateStepSummary(x, serviceName))
                .ToArray(),
            Result = registration.Result is null
                ? null
                : CreateResultMetadata(registration.Result),
            ExecuteUrl = ProcessContractUrls.ExecuteStep(serviceName, stepName),
            MetadataUrl = ProcessContractUrls.StepMetadata(serviceName, stepName)
        };
    }

    public ProcessStepSummary CreateStepSummary(
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

    private static ProcessFieldMetadata CreateFieldMetadata(
        ProcessorInputFieldDescriptor item)
    {
        return new ProcessFieldMetadata
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType,
            Constraints = item.Constraints
        };
    }

    private static ProcessStepResultMetadata CreateResultMetadata(
        ProcessorStepResultDescriptor item)
    {
        return new ProcessStepResultMetadata
        {
            OutputFields = item.OutputFields
                .Select(CreateOutputFieldMetadata)
                .ToArray()
        };
    }

    private static ProcessOutputFieldMetadata CreateOutputFieldMetadata(
        ProcessorOutputFieldDescriptor item)
    {
        return new ProcessOutputFieldMetadata
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType
        };
    }
}
