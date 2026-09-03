using Kaleido.Process.Participant.Registry;
using System.ComponentModel;
using System.Reflection;

namespace Kaleido.Process.Participant.Registry;

internal sealed class ParticipantRegistry : IParticipantRegistry
{
    private readonly IReadOnlyCollection<ParticipantRegistryItem> _registrations;
    private readonly IReadOnlyDictionary<string, ParticipantRegistryItem> _byName;

    public ParticipantRegistry(
        ParticipantOptions options,
        IProcessStepRegistry stepRegistry)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(stepRegistry);

        _registrations =
        [
            ParticipantRegistryProjection.Project(
                options,
                stepRegistry.InitialRegistrations,
                stepRegistry.Registrations)
        ];

        _byName =
            _registrations.ToDictionary(
                x => x.Name,
                StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<ParticipantRegistryItem> Registrations =>
        _registrations;

    public ParticipantRegistryItem? Find(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _byName.TryGetValue(name, out var registration);
        return registration;
    }

    public ParticipantRegistryItem GetRegistration(
        string name) =>
        Find(name)
        ?? throw new KeyNotFoundException(
            $"Participant registry item '{name}' is not registered.");
}

internal static class ParticipantRegistryProjection
{
    public static ParticipantRegistryItem Project(
        ParticipantOptions options,
        IReadOnlyCollection<ProcessStepRegistration> initialSteps,
        IReadOnlyCollection<ProcessStepRegistration> steps)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(initialSteps);
        ArgumentNullException.ThrowIfNull(steps);

        return new ParticipantRegistryItem
        {
            Name = options.Name,
            Description = options.Description,
            DisplayName = options.DisplayName,
            Version = options.Version,
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

    public static ParticipantStepRegistryItem Project(
        ProcessStepRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new ParticipantStepRegistryItem
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

    public static ParticipantStepSummary ProjectSummary(
        ProcessStepRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new ParticipantStepSummary
        {
            Name = registration.Metadata.Name,
            Description = registration.Metadata.Description,
            DisplayName = registration.Metadata.DisplayName,
            Version = registration.Metadata.Version,
            Repeatable = registration.Repeatable.Enabled
        };
    }

    public static ParticipantInputFieldDescriptor ProjectInput(
        PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return new ParticipantInputFieldDescriptor
        {
            Name = property.Name,
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description,
            DataType = DataTypeMapper.GetDescriptor(property),
            Constraints = ConstraintMapper.Map(property)
        };
    }

    public static ParticipantStepResultDescriptor? ProjectResult(
        Type? resultType)
    {
        if (resultType is null)
        {
            return null;
        }

        return new ParticipantStepResultDescriptor
        {
            OutputFields = GetResultProperties(resultType)
                .Select(ProjectOutput)
                .ToArray()
        };
    }

    public static ParticipantOutputFieldDescriptor ProjectOutput(
        PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        return new ParticipantOutputFieldDescriptor
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
