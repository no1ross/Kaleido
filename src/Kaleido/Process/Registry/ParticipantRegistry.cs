using Kaleido;
using System.ComponentModel;
using System.Reflection;

namespace Kaleido.Process.Registry;

internal sealed class ProcessorRegistry : IProcessorRegistry
{
    private readonly IReadOnlyCollection<ProcessorRegistryItem> _registrations;

    public ProcessorRegistry(
        KaleidoServiceOptions serviceOptions,
        IProcessStepRegistry stepRegistry)
    {
        ArgumentNullException.ThrowIfNull(serviceOptions);
        ArgumentNullException.ThrowIfNull(stepRegistry);

        _registrations =
        [
            ProcessorRegistryProjection.Project(
                serviceOptions,
                stepRegistry.InitialRegistrations,
                stepRegistry.Registrations)
        ];
    }

    public IReadOnlyCollection<ProcessorRegistryItem> Registrations =>
        _registrations;
}

internal static class ProcessorRegistryProjection
{
    internal static ProcessorRegistryItem Project(
        KaleidoServiceOptions serviceOptions,
        IReadOnlyCollection<ProcessStepRegistration> initialSteps,
        IReadOnlyCollection<ProcessStepRegistration> steps)
    {
        ArgumentNullException.ThrowIfNull(serviceOptions);
        ArgumentNullException.ThrowIfNull(initialSteps);
        ArgumentNullException.ThrowIfNull(steps);

        return new ProcessorRegistryItem
        {
            IsEntryProcessor = serviceOptions.IsEntryProcessor,
            InitialSteps = initialSteps
                .OrderBy(x => x.Metadata.Name)
                .Select(ProjectSummary)
                .ToArray(),
            Steps = steps
                .OrderBy(x => x.Metadata.Name)
                .Select(Project)
                .ToArray()
        };
    }

    internal static ProcessorStepRegistryItem Project(
        ProcessStepRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new ProcessorStepRegistryItem
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            DisplayName = registration.Metadata.DisplayName,
            Version = registration.Metadata.Version,
            Repeatable = registration.Repeatable.Enabled,
            Fields = registration.StepType
                .GetProperties()
                .Select(ProjectInput)
                .ToArray(),
            Dependencies = registration.Dependencies
                .OrderBy(x => x.Metadata.Name)
                .Select(ProjectSummary)
                .ToArray(),
            AvailableAfter = registration.AvailableAfter
                .OrderBy(x => x.Metadata.Name)
                .Select(ProjectSummary)
                .ToArray(),
            AvailableUntil = registration.AvailableUntil
                .OrderBy(x => x.Metadata.Name)
                .Select(ProjectSummary)
                .ToArray(),
            Result = ProjectResult(registration.StepResultType)
        };
    }

    internal static ProcessorStepSummary ProjectSummary(
        ProcessStepRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new ProcessorStepSummary
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            DisplayName = registration.Metadata.DisplayName,
            Version = registration.Metadata.Version,
            Repeatable = registration.Repeatable.Enabled
        };
    }

    internal static ProcessorInputFieldDescriptor ProjectInput(
        PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return new ProcessorInputFieldDescriptor
        {
            Name = property.Name,
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            DataType = DataTypeMapper.GetDescriptor(property),
            Constraints = ConstraintMapper.Map(property)
        };
    }

    internal static ProcessorStepResultDescriptor? ProjectResult(
        Type? resultType)
    {
        if (resultType is null)
        {
            return null;
        }

        return new ProcessorStepResultDescriptor
        {
            OutputFields = GetResultProperties(resultType)
                .Select(ProjectOutput)
                .ToArray()
        };
    }

    internal static ProcessorOutputFieldDescriptor ProjectOutput(
        PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return new ProcessorOutputFieldDescriptor
        {
            Name = property.Name,
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            DataType = DataTypeMapper.GetDescriptor(property)
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
