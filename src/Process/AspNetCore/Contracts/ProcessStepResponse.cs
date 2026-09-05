using Kaleido.Process.Registry;

namespace Kaleido.Process.AspNetCore.Contracts;

// Wire shapes (ProcessorRegistryResponse, ProcessStepResponse, ProcessStepSummary, etc.)
// are defined in Kaleido.Process.AspNetCore.Abstractions. Factory methods that depend
// on server-side types (ProcessRouteOptions, ProcessorRegistryItem) live here as extensions.

public static class ProcessorRegistryResponseFactory
{
    public static ProcessorRegistryResponse FromRegistration(
        ProcessorRegistryItem registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        return new ProcessorRegistryResponse
        {
            Name = registration.Name,
            Description = registration.Description,
            DisplayName = registration.DisplayName,
            Version = registration.Version,
            RegistryUrl = ProcessContractUrls.Registry(options),
            InitialSteps = registration.InitialSteps
                .Select(x => ProcessStepResponseFactory.ToSummary(x, options))
                .ToArray(),
            Steps = registration.Steps
                .Select(x => ProcessStepResponseFactory.FromRegistration(x, options))
                .ToArray()
        };
    }
}

public static class ProcessorCatalogResponseFactory
{
    public static ProcessorCatalogResponse FromRegistration(
        ProcessorRegistryItem registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        return new ProcessorCatalogResponse
        {
            Name = registration.Name,
            Description = registration.Description,
            DisplayName = registration.DisplayName,
            Version = registration.Version,
            RegistryUrl = ProcessContractUrls.Registry(options),
            InitialSteps = registration.InitialSteps
                .Select(x => ProcessStepResponseFactory.ToSummary(x, options))
                .ToArray()
        };
    }
}

public static class ProcessStepResponseFactory
{
    public static ProcessStepResponse FromRegistration(
        ProcessorStepRegistryItem registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

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
                .Select(x => ToSummary(x, options))
                .ToArray(),
            AvailableAfter = registration.AvailableAfter
                .OrderBy(x => x.Name)
                .Select(x => ToSummary(x, options))
                .ToArray(),
            AvailableUntil = registration.AvailableUntil
                .OrderBy(x => x.Name)
                .Select(x => ToSummary(x, options))
                .ToArray(),
            Result = registration.Result is null
                ? null
                : ProcessStepResultMetadata.FromRegistryItem(registration.Result),
            ExecuteUrl = ProcessContractUrls.ExecuteStep(
                options,
                stepName),
            MetadataUrl = ProcessContractUrls.StepMetadata(
                options,
                stepName)
        };
    }

    internal static ProcessStepSummary ToSummary(
        ProcessorStepSummary registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var stepName =
            registration.Name.ToLowerInvariant();

        return new ProcessStepSummary
        {
            Name = registration.Name,
            Description = registration.Description,
            DisplayName = registration.DisplayName,
            Version = registration.Version,
            Repeatable = registration.Repeatable,
            ExecuteUrl = ProcessContractUrls.ExecuteStep(
                options,
                stepName),
            MetadataUrl = ProcessContractUrls.StepMetadata(
                options,
                stepName)
        };
    }
}
