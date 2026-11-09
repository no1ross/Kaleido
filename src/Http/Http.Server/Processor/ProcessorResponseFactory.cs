using Kaleido.Http.Server.Registry;
using Kaleido.Processor;
using Kaleido.Processor.Registry;

namespace Kaleido.Http.Server.Processor;

/// <summary>
/// Projects process runtime registry types into HTTP wire contract types.
/// Injectable so endpoint and service tests can mock the projection boundary.
/// </summary>
internal interface IProcessorResponseFactory
{
    ProcessorRegistryResponse CreateRegistryResponse(ProcessorRegistryItem registration);

    ProcessStepResponse CreateStepResponse(ProcessorStepRegistryItem registration);

    ProcessStepSummary CreateStepSummary(ProcessorStepSummary registration);

    ProcessRequiredStep CreateRequiredStep(ProcessorStepSummary registration, InformationRequest? informationRequest);
}

internal sealed class ProcessorResponseFactory(KaleidoServiceOptions serviceOptions) : IProcessorResponseFactory
{
    private string Version => serviceOptions.ServiceVersion ?? throw new KaleidoConfigurationException(
        ConfigurationErrorCodes.InvalidServiceVersion, "A Kaleido HTTP service must name the version in its published routes.");

    public ProcessorRegistryResponse CreateRegistryResponse(ProcessorRegistryItem registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var serviceName = serviceOptions.ServiceName;
        var version = Version;

        return new ProcessorRegistryResponse
        {
            ServiceName = serviceName,
            ServiceVersion = serviceOptions.ServiceVersion,
            Name = serviceName,
            Description = serviceOptions.Description,
            DisplayName = serviceOptions.DisplayName,
            IsEntryProcessor = registration.IsEntryProcessor,
            ExecuteUrl = ProcessContractUrls.Execute(serviceName, version),
            ProcessStateUrlTemplate = ProcessContractUrls.ProcessStateTemplate(serviceName, version),
            InitialSteps = registration.InitialSteps
                .Select(CreateStepSummary)
                .ToArray(),
            Steps = registration.Steps
                .Select(CreateStepResponse)
                .ToArray(),
        };
    }

    public ProcessStepResponse CreateStepResponse(ProcessorStepRegistryItem registration)
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
                .Select(CreateStepSummary)
                .ToArray(),
            AvailableAfter = registration.AvailableAfter
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(CreateStepSummary)
                .ToArray(),
            AvailableUntil = registration.AvailableUntil
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(CreateStepSummary)
                .ToArray(),
            Result = registration.Result is null
                ? null
                : CreateResultMetadata(registration.Result),
            ExecuteUrl = ProcessContractUrls.ExecuteStep(serviceOptions.ServiceName, Version, stepName),
        };
    }

    public ProcessStepSummary CreateStepSummary(ProcessorStepSummary registration)
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
            ExecuteUrl = ProcessContractUrls.ExecuteStep(serviceOptions.ServiceName, Version, stepName),
        };
    }

    public ProcessRequiredStep CreateRequiredStep(
        ProcessorStepSummary registration,
        InformationRequest? informationRequest)
    {
        var summary = CreateStepSummary(registration);

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
