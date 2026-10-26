namespace Kaleido.Processor.Registry;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

internal sealed partial record ProcessStepRegistration
{
    internal ProcessorStepRegistryItem ToRegistryItem(
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper)
    {
        return new ProcessorStepRegistryItem
        {
            Name = Metadata.Name,
            Description = Metadata.Description,
            DisplayName = Metadata.DisplayName,
            Repeatable = Repeatable.Enabled,
            IsInformationStep = IsInformationStep,
            Destructive = Metadata.Destructive,
            External = Metadata.External,
            RequiresConfirmation = Metadata.RequiresConfirmation,
            Authorization = Metadata.Authorization,
            // An information step's input is described by its pending InformationRequest,
            // not by its fixed InformationRequestId/Items shape.
            Fields = IsInformationStep ? [] : ToInputDescriptors(typeDescriber, constraintMapper),
            Dependencies = Dependencies
                .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.ToSummary())
                .ToArray(),
            AvailableAfter = AvailableAfter
                .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.ToSummary())
                .ToArray(),
            AvailableUntil = AvailableUntil
                .OrderBy(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.ToSummary())
                .ToArray(),
            Result = ToResultDescriptor(typeDescriber),
        };
    }

    /// <summary>
    /// The step as a summary: how it is named from elsewhere (initial steps, relationships, next and
    /// required steps), with its declared behavior and effective authorization.
    /// </summary>
    /// <returns>The step's <see cref="ProcessorStepSummary"/>.</returns>
    public ProcessorStepSummary ToSummary()
    {
        return new ProcessorStepSummary
        {
            Name = Metadata.Name,
            Description = Metadata.Description,
            DisplayName = Metadata.DisplayName,
            Repeatable = Repeatable.Enabled,
            IsInformationStep = IsInformationStep,
            Destructive = Metadata.Destructive,
            External = Metadata.External,
            RequiresConfirmation = Metadata.RequiresConfirmation,
            Authorization = Metadata.Authorization,
        };
    }

    private ProcessorInputFieldDescriptor[] ToInputDescriptors(
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper)
    {
        var properties = StepType.GetProperties();

        // A group's display fields may be declared on any of its inputs; every input of the group
        // publishes them all, in declaration order.
        var displays = properties
            .Select(property => property.GetCustomAttribute<LookupAttribute>())
            .OfType<LookupAttribute>()
            .GroupBy(LookupGroup, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)x.SelectMany(l => l.Display).Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);

        return properties
            .Select(property =>
                ToInputDescriptor(
                    property,
                    typeDescriber,
                    constraintMapper,
                    property.GetCustomAttribute<LookupAttribute>() is { } lookup
                        ? new LookupMetadata
                        {
                            Service = lookup.Service,
                            Source = lookup.Source,
                            View = string.IsNullOrWhiteSpace(lookup.View) ? null : lookup.View,
                            Field = lookup.Field,
                            Group = LookupGroup(lookup),
                            Display = displays[LookupGroup(lookup)],
                        }
                        : null))
            .ToArray();
    }

    private static string LookupGroup(LookupAttribute lookup) =>
        !string.IsNullOrWhiteSpace(lookup.Group)
            ? lookup.Group
            : string.IsNullOrWhiteSpace(lookup.View)
                ? $"{lookup.Service}/{lookup.Source}"
                : $"{lookup.Service}/{lookup.Source}/{lookup.View}";

    private static ProcessorInputFieldDescriptor ToInputDescriptor(
        PropertyInfo property,
        ITypeDescriber typeDescriber,
        IConstraintMapper constraintMapper,
        LookupMetadata? lookup)
    {
        var display = property.GetCustomAttribute<DisplayAttribute>();

        return new ProcessorInputFieldDescriptor
        {
            Name = property.Name,
            DisplayName = display?.GetName(),
            Prompt = display?.GetPrompt(),
            Description =
                property.GetCustomAttribute<DescriptionAttribute>()?.Description
                ?? display?.GetDescription(),
            DataType = typeDescriber.GetDescriptor(property),
            Classifications = property.GetDataClassificationNames(),
            Lookup = lookup,
            Constraints = constraintMapper.Map(property),
        };
    }

    private ProcessorStepResultDescriptor? ToResultDescriptor(
        ITypeDescriber typeDescriber)
    {
        if (StepResultType is null)
        {
            return null;
        }

        return new ProcessorStepResultDescriptor
        {
            OutputFields = GetResultProperties(StepResultType)
                .Select(property =>
                    ToOutputDescriptor(
                        property,
                        typeDescriber))
                .ToArray(),
        };
    }

    private static ProcessorOutputFieldDescriptor ToOutputDescriptor(
        PropertyInfo property,
        ITypeDescriber typeDescriber)
    {
        return new ProcessorOutputFieldDescriptor
        {
            Name = property.Name,
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            DataType = typeDescriber.GetDescriptor(property),
            Classifications = property.GetDataClassificationNames(),
        };
    }

    private static PropertyInfo[] GetResultProperties(
        Type resultType)
    {
        if (resultType == typeof(string)
            || resultType.IsPrimitive
            || resultType.IsEnum)
        {
            return [];
        }

        return resultType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.GetMethod is not null)
            .ToArray();
    }
}
