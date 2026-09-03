using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ParticipantRegistryResponse : ParticipantRegistryItem
{
    public string RegistryUrl { get; init; }
        = string.Empty;

    public new IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; }
        = [];

    public new IReadOnlyCollection<ProcessStepResponse> Steps { get; init; }
        = [];

    public static ParticipantRegistryResponse FromRegistration(
        ParticipantRegistryItem registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        return new ParticipantRegistryResponse
        {
            Name = registration.Name,
            Description = registration.Description,
            DisplayName = registration.DisplayName,
            Version = registration.Version,
            RegistryUrl = ProcessContractUrls.Registry(options),
            InitialSteps = registration.InitialSteps
                .Select(x => ProcessStepResponse.ToSummary(x, options))
                .ToArray(),
            Steps = registration.Steps
                .Select(x => ProcessStepResponse.FromRegistration(x, options))
                .ToArray()
        };
    }
}

public sealed record ProcessCatalogResponse
{
    public IReadOnlyCollection<ParticipantCatalogResponse> Participants
    {
        get;
        init;
    }
        = [];
}

public sealed record ParticipantCatalogResponse
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? DisplayName { get; init; }

    public string? Version { get; init; }

    public string RegistryUrl { get; init; }
        = string.Empty;

    public IReadOnlyCollection<ProcessStepSummary> InitialSteps { get; init; }
        = [];

    public static ParticipantCatalogResponse FromRegistration(
        ParticipantRegistryItem registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        return new ParticipantCatalogResponse
        {
            Name = registration.Name,
            Description = registration.Description,
            DisplayName = registration.DisplayName,
            Version = registration.Version,
            RegistryUrl = ProcessContractUrls.Registry(options),
            InitialSteps = registration.InitialSteps
                .Select(x => ProcessStepResponse.ToSummary(x, options))
                .ToArray()
        };
    }
}

public sealed record ProcessStepResponse : ParticipantStepRegistryItem
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

    public static ProcessStepResponse FromRegistration(
        ParticipantStepRegistryItem registration,
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
        ParticipantStepSummary registration,
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

public sealed record ProcessStepSummary : ParticipantStepSummary
{
    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;
}

public sealed record ProcessFieldMetadata : ParticipantInputFieldDescriptor
{
    public static ProcessFieldMetadata FromRegistryItem(
        ParticipantInputFieldDescriptor item)
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

public sealed record ProcessOutputFieldMetadata : ParticipantOutputFieldDescriptor
{
    public static ProcessOutputFieldMetadata FromRegistryItem(
        ParticipantOutputFieldDescriptor item)
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

public sealed record ProcessStepResultMetadata : ParticipantStepResultDescriptor
{
    public new IReadOnlyCollection<ProcessOutputFieldMetadata> OutputFields { get; init; }
        = [];

    public static ProcessStepResultMetadata FromRegistryItem(
        ParticipantStepResultDescriptor item)
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
