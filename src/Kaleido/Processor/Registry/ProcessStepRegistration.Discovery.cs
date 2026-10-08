namespace Kaleido.Processor.Registry;

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

public sealed partial record ProcessStepRegistration
{
    internal ProcessorStepRegistryItem ToRegistryItem(
        ITypeDescriber TypeDescriber,
        IConstraintMapper constraintMapper)
    {
        return new ProcessorStepRegistryItem
        {
            Name = Metadata.Name,
            Description = Metadata.Description,
            DisplayName = Metadata.DisplayName,
            Version = Metadata.Version,
            Repeatable = Repeatable.Enabled,
            IsInformationStep = IsInformationStep,
            Authorization = Metadata.Authorization,
            // An information step's input is described by its pending InformationRequest,
            // not by its fixed InformationRequestId/Items shape.
            Fields = IsInformationStep ? [] : StepType
                .GetProperties()
                .Select(property =>
                    ToInputDescriptor(
                        property,
                        TypeDescriber,
                        constraintMapper))
                .ToArray(),
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
            Result = ToResultDescriptor(TypeDescriber)
        };
    }

    internal ProcessorStepSummary ToSummary()
    {
        return new ProcessorStepSummary
        {
            Name = Metadata.Name,
            Description = Metadata.Description,
            DisplayName = Metadata.DisplayName,
            Version = Metadata.Version,
            Repeatable = Repeatable.Enabled,
            IsInformationStep = IsInformationStep,
            Authorization = Metadata.Authorization
        };
    }

    private static ProcessorInputFieldDescriptor ToInputDescriptor(
        PropertyInfo property,
        ITypeDescriber TypeDescriber,
        IConstraintMapper constraintMapper)
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
            DataType = TypeDescriber.GetDescriptor(property),
            Constraints = constraintMapper.Map(property)
        };
    }

    private ProcessorStepResultDescriptor? ToResultDescriptor(
        ITypeDescriber TypeDescriber)
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
                        TypeDescriber))
                .ToArray()
        };
    }

    private static ProcessorOutputFieldDescriptor ToOutputDescriptor(
        PropertyInfo property,
        ITypeDescriber TypeDescriber)
    {
        return new ProcessorOutputFieldDescriptor
        {
            Name = property.Name,
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            DataType = TypeDescriber.GetDescriptor(property)
        };
    }

    private static IReadOnlyCollection<PropertyInfo> GetResultProperties(
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
