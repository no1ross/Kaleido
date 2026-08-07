using Kaleido.Process.Participant.Registry;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessStepResponse
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public bool Repeatable { get; init; }

    public IReadOnlyCollection<ProcessFieldMetadata> Fields { get; init; }
        = Array.Empty<ProcessFieldMetadata>();

    public IReadOnlyCollection<ProcessStepSummary> Dependencies { get; init; }
        = Array.Empty<ProcessStepSummary>();

    public IReadOnlyCollection<ProcessStepSummary> AvailableAfter { get; init; }
        = Array.Empty<ProcessStepSummary>();

    public IReadOnlyCollection<ProcessStepSummary> AvailableUntil { get; init; }
        = Array.Empty<ProcessStepSummary>();

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;

    public static ProcessStepResponse FromRegistration(
        ProcessStepRegistration registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var stepName =
            registration.Metadata.Name.ToLowerInvariant();

        return new ProcessStepResponse
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            DisplayName = registration.Metadata.DisplayName,
            Version = registration.Metadata.Version,
            Repeatable = registration.Repeatable.Enabled,

            Fields = registration.StepType
                .GetProperties()
                .Select(ProcessFieldMetadataFactory.FromProperty)
                .ToArray(),

            Dependencies = registration.Dependencies
                .OrderBy(x => x.Metadata.Name)
                .Select(x => ToSummary(x, options))
                .ToArray(),

            AvailableAfter = registration.AvailableAfter
                .OrderBy(x => x.Metadata.Name)
                .Select(x => ToSummary(x, options))
                .ToArray(),

            AvailableUntil = registration.AvailableUntil
                .OrderBy(x => x.Metadata.Name)
                .Select(x => ToSummary(x, options))
                .ToArray(),

            ExecuteUrl = ProcessContractUrls.ExecuteStep(
                options,
                stepName),

            MetadataUrl = ProcessContractUrls.StepMetadata(
                options,
                stepName)
        };
    }

    internal static ProcessStepSummary ToSummary(
        ProcessStepRegistration registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var stepName =
            registration.Metadata.Name.ToLowerInvariant();

        return new ProcessStepSummary
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            DisplayName = registration.Metadata.DisplayName,
            Version = registration.Metadata.Version,
            Repeatable = registration.Repeatable.Enabled,

            ExecuteUrl = ProcessContractUrls.ExecuteStep(
                options,
                stepName),

            MetadataUrl = ProcessContractUrls.StepMetadata(
                options,
                stepName)
        };
    }
}

public sealed record ProcessStepSummary
{
    public required string Name { get; init; }

    public required string Version { get; init; }

    public string? DisplayName { get; init; }

    public string? Description { get; init; }

    public bool Repeatable { get; init; }

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;
}

public sealed record ProcessFieldMetadata
{
    public required string Name { get; init; }

    public required DataTypeDescriptor DataType { get; init; }

    public IReadOnlyCollection<ConstraintContract> Constraints
    {
        get;
        init;
    }
        = [];
}

internal static class ProcessFieldMetadataFactory
{
    public static ProcessFieldMetadata FromProperty(
        PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return new ProcessFieldMetadata
        {
            Name = property.Name,

            DataType =
                DataTypeMapper.GetDescriptor(
                    property.PropertyType),

            Constraints =
                ConstraintMapper.Map(property)
        };
    }
}

