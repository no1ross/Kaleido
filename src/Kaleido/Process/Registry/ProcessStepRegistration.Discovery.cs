namespace Kaleido.Process.Registry;

using System.ComponentModel;
using System.Reflection;

public sealed partial record ProcessStepRegistration
{
    internal ProcessorStepRegistryItem ToRegistryItem(
        IDataTypeMapper dataTypeMapper,
        IConstraintMapper constraintMapper)
    {
        return new ProcessorStepRegistryItem
        {
            Name = Metadata.Name,
            Description = Metadata.Description,
            DisplayName = Metadata.DisplayName,
            Version = Metadata.Version,
            Repeatable = Repeatable.Enabled,
            Fields = StepType
                .GetProperties()
                .Select(property =>
                    ToInputDescriptor(
                        property,
                        dataTypeMapper,
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
            Result = ToResultDescriptor(dataTypeMapper)
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
            Repeatable = Repeatable.Enabled
        };
    }

    private static ProcessorInputFieldDescriptor ToInputDescriptor(
        PropertyInfo property,
        IDataTypeMapper dataTypeMapper,
        IConstraintMapper constraintMapper)
    {
        return new ProcessorInputFieldDescriptor
        {
            Name = property.Name,
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            DataType = dataTypeMapper.GetDescriptor(property),
            Constraints = constraintMapper.Map(property)
        };
    }

    private ProcessorStepResultDescriptor? ToResultDescriptor(
        IDataTypeMapper dataTypeMapper)
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
                        dataTypeMapper))
                .ToArray()
        };
    }

    private static ProcessorOutputFieldDescriptor ToOutputDescriptor(
        PropertyInfo property,
        IDataTypeMapper dataTypeMapper)
    {
        return new ProcessorOutputFieldDescriptor
        {
            Name = property.Name,
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            DataType = dataTypeMapper.GetDescriptor(property)
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
