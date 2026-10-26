using Kaleido.Http.Registry;
using Kaleido.Processor;
using Kaleido.Processor.Registry;

namespace Kaleido.Http.Processor;

/// <summary>
/// Projects process runtime registry types into HTTP wire contract types.
/// Injectable so endpoint and service tests can mock the projection boundary.
/// </summary>
internal interface IProcessorResponseFactory
{
    ProcessorRegistryResponse CreateRegistryResponse(
        ProcessorRegistryItem registration,
        KaleidoServiceOptions serviceOptions);

    ProcessStepResponse CreateStepResponse(
        ProcessorStepRegistryItem registration,
        string serviceName);

    ProcessStepSummary CreateStepSummary(
        ProcessorStepSummary registration,
        string serviceName);

    ProcessRequiredStep CreateRequiredStep(
        ProcessorStepSummary registration,
        InformationRequest? informationRequest,
        string serviceName);
}

internal sealed class ProcessorResponseFactory : IProcessorResponseFactory
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
            RegistryUrl = RegistryContractUrls.Registry(serviceName),
            InitialSteps = registration.InitialSteps
                .Select(x => CreateStepSummary(x, serviceName))
                .ToArray(),
            Steps = registration.Steps
                .Select(x => CreateStepResponse(x, serviceName))
                .ToArray(),
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
            Repeatable = registration.Repeatable,
            IsInformationStep = registration.IsInformationStep,
            Destructive = registration.Destructive,
            External = registration.External,
            RequiresConfirmation = registration.RequiresConfirmation,
            IsInitial =
                registration.Dependencies.Count == 0 &&
                registration.AvailableAfter.Count == 0,
            Authorization = registration.Authorization,
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
            Repeatable = registration.Repeatable,
            IsInformationStep = registration.IsInformationStep,
            Destructive = registration.Destructive,
            External = registration.External,
            RequiresConfirmation = registration.RequiresConfirmation,
            Authorization = registration.Authorization,
            ExecuteUrl = ProcessContractUrls.ExecuteStep(serviceName, stepName),
        };
    }

    public ProcessRequiredStep CreateRequiredStep(
        ProcessorStepSummary registration,
        InformationRequest? informationRequest,
        string serviceName)
    {
        var summary =
            CreateStepSummary(
                registration,
                serviceName);

        return new ProcessRequiredStep
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
            ExecuteUrl = summary.ExecuteUrl,
            InformationRequest = informationRequest,
        };
    }

    private static ProcessFieldMetadata CreateFieldMetadata(
        ProcessorInputFieldDescriptor item)
    {
        return new ProcessFieldMetadata
        {
            Name = item.Name,
            DisplayName = item.DisplayName,
            Prompt = item.Prompt,
            Lookup = item.Lookup.ToWire(),
            Description = item.Description,
            DataType = item.DataType.ToWire(),
            Classifications = item.Classifications,
            Constraints = item.Constraints.ToWire(),
        };
    }

    private static ProcessStepResultMetadata CreateResultMetadata(
        ProcessorStepResultDescriptor item)
    {
        return new ProcessStepResultMetadata
        {
            OutputFields = item.OutputFields
                .Select(CreateOutputFieldMetadata)
                .ToArray(),
        };
    }

    private static ProcessOutputFieldMetadata CreateOutputFieldMetadata(
        ProcessorOutputFieldDescriptor item)
    {
        return new ProcessOutputFieldMetadata
        {
            Name = item.Name,
            Description = item.Description,
            DataType = item.DataType.ToWire(),
            Classifications = item.Classifications,
        };
    }
}
