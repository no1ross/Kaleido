using System.ComponentModel;
using System.Reflection;

namespace Kaleido.Process.Registry;

internal sealed class ProcessorRegistry : IProcessorRegistry
{
    private readonly IReadOnlyCollection<ProcessorRegistryItem> _registrations;
    private readonly IReadOnlyDictionary<string, ProcessorRegistryItem> _byName;

    public ProcessorRegistry(
        ProcessorOptions options,
        IProcessStepRegistry stepRegistry)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stepRegistry);

        _registrations =
        [
            ProcessorRegistryProjection.Project(
                options,
                stepRegistry.InitialRegistrations,
                stepRegistry.Registrations)
        ];

        _byName =
            _registrations.ToDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ProcessorRegistryItem> Registrations =>
        _registrations;

    public ProcessorRegistryItem? Find(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _byName.TryGetValue(name, out var registration);
        return registration;
    }

    public ProcessorRegistryItem GetRegistration(
        string name) =>
        Find(name)
        ?? throw new KeyNotFoundException(
            $"Processor registry item '{name}' is not registered.");
}

internal static class ProcessorRegistryProjection
{
    public static ProcessorRegistryItem Project(
        ProcessorOptions options,
        IReadOnlyCollection<ProcessStepRegistration> initialSteps,
        IReadOnlyCollection<ProcessStepRegistration> steps)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(initialSteps);
        ArgumentNullException.ThrowIfNull(steps);

        return new ProcessorRegistryItem
        {
            Name = options.Name,
            Description = options.Description,
            DisplayName = options.DisplayName,
            Version = options.Version,
            InstanceId = options.InstanceId,
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

    public static ProcessorStepRegistryItem Project(
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

    public static ProcessorStepSummary ProjectSummary(
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

    public static ProcessorInputFieldDescriptor ProjectInput(
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

    public static ProcessorStepResultDescriptor? ProjectResult(
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

    public static ProcessorOutputFieldDescriptor ProjectOutput(
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
