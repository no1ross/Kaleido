using Kaleido.Process.Participant.Registry;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessStepContract
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    public IReadOnlyCollection<AttributeContract> Attributes { get; init; }
        = Array.Empty<AttributeContract>();

    public IReadOnlyCollection<ProcessStepSummaryContract> Dependencies { get; init; }
        = Array.Empty<ProcessStepSummaryContract>();

    public IReadOnlyCollection<ProcessStepSummaryContract> AvailableAfter { get; init; }
        = Array.Empty<ProcessStepSummaryContract>();

    public IReadOnlyCollection<ProcessStepSummaryContract> AvailableUntil { get; init; }
        = Array.Empty<ProcessStepSummaryContract>();

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;

    public static ProcessStepContract FromRegistration(
        ProcessStepRegistration registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var stepName =
            registration.Metadata.Name.ToLowerInvariant();

        return new ProcessStepContract
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            Version = registration.Metadata.Version,
            Repeatable = registration.Repeatable.Enabled,

            Attributes = registration.StepType
                .GetProperties()
                .Select(AttributeContractFactory.FromProperty)
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

    internal static ProcessStepSummaryContract ToSummary(
        ProcessStepRegistration registration,
        ProcessRouteOptions options)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(options);

        var stepName =
            registration.Metadata.Name.ToLowerInvariant();

        return new ProcessStepSummaryContract
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
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

public sealed record ProcessStepSummaryContract
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Version { get; init; }

    public bool Repeatable { get; init; }

    public string ExecuteUrl { get; init; }
        = string.Empty;

    public string MetadataUrl { get; init; }
        = string.Empty;
}

public sealed record AttributeContract
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

internal static class AttributeContractFactory
{
    public static AttributeContract FromProperty(
        PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return new AttributeContract
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

