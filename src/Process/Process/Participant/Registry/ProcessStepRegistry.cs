using Kaleido.Process.Attributes;
using Kaleido.Process.Participant.Execution;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Process.Participant.Registry;

internal sealed class ProcessStepRegistry : IProcessStepRegistry
{
    private readonly IReadOnlyDictionary<string, ProcessStepRegistration> _byName;

    private readonly IReadOnlyDictionary<Type, ProcessStepRegistration> _byType;

    private readonly IReadOnlyCollection<ProcessStepRegistration> _registrations;

    public ProcessStepRegistry(
        IServiceCollection services,
        IEnumerable<Type> stepTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(stepTypes);

        var stepTypeArray =
            stepTypes
                .Distinct()
                .ToArray();

        // Pass 1
        var typeDefinitions =
            stepTypeArray
                .Select(stepType =>
                    BuildTypeDefinition(
                        services,
                        stepType))
                .ToArray();

        var typeDefinitionsByType =
            typeDefinitions.ToDictionary(
                x => x.StepType);

        // Pass 2a
        var definitions =
            typeDefinitions
                .Select(x =>
                    new ProcessStepDefinition
                    {
                        StepType = x.StepType,
                        StepResultType = x.StepResultType,
                        HandlerType = x.HandlerType,
                        Metadata = x.Metadata
                    })
                .ToArray();

        var definitionsByType =
            definitions.ToDictionary(
                x => x.StepType);

        // Pass 2b
        foreach (var definition in definitions)
        {
            var typeDefinition =
                typeDefinitionsByType[
                    definition.StepType];

            HydrateDefinition(
                definition,
                typeDefinition,
                definitionsByType);
        }

        // Pass 3
        RegistrationValidator.Validate(
            definitions);

        // Pass 4
        var registrations =
            BuildRegistrations(
                definitions);

        _registrations =
            registrations;

        _byName =
            registrations.ToDictionary(
                x => x.Metadata.Name,
                StringComparer.OrdinalIgnoreCase);

        _byType =
            registrations.ToDictionary(
                x => x.StepType);
    }

    public IReadOnlyCollection<ProcessStepRegistration> Registrations =>
        _registrations;

    public ProcessStepRegistration? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _byName.TryGetValue(
            name,
            out var registration);

        return registration;
    }

    public ProcessStepRegistration? Find(Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        _byType.TryGetValue(
            stepType,
            out var registration);

        return registration;
    }

    public ProcessStepRegistration GetRegistration(string name)
    {
        return Find(name)
            ?? throw new KeyNotFoundException(
                $"Process step '{name}' is not registered.");
    }

    public ProcessStepRegistration GetRegistration(Type stepType)
    {
        return Find(stepType)
            ?? throw new KeyNotFoundException(
                $"Process step type '{stepType.FullName}' is not registered.");
    }

    private static ProcessStepTypeDefinition BuildTypeDefinition(
        IServiceCollection services,
        Type stepType)
    {
        var handlerType =
            GetHandlerType(
                services,
                stepType);

        var handlerInterface =
            handlerType
                .GetInterfaces()
                .Single(i =>
                    i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(IProcessStepHandler<,>)
                    && i.GetGenericArguments()[0] == stepType);

        var resultType =
            handlerInterface.GetGenericArguments()[1];

        var metadata =
            BuildStepMetadata(
                stepType);

        var definition =
            new ProcessStepTypeDefinition
            {
                StepType = stepType,
                StepResultType = resultType,
                HandlerType = handlerType,
                Metadata = metadata
            };

        foreach (var dependency in
            stepType.GetCustomAttributes<DependsOnStepAttribute>())
        {
            definition.Dependencies.Add(
                dependency.DependsOnStep);
        }

        foreach (var availableAfter in
            stepType.GetCustomAttributes<AvailableAfterAttribute>())
        {
            definition.AvailableAfter.Add(
                availableAfter.AvailableAfterStep);
        }

        foreach (var availableUntil in
            stepType.GetCustomAttributes<AvailableUntilAttribute>())
        {
            definition.AvailableUntil.Add(
                availableUntil.AvailableUntilStep);
        }

        return definition;
    }

    private static IReadOnlyCollection<ProcessStepRegistration> BuildRegistrations(
        IReadOnlyCollection<ProcessStepDefinition> definitions)
    {
        var registrations =
            new Dictionary<Type, ProcessStepRegistration>();

        foreach (var definition in definitions)
        {
            CreateRegistration(
                definition,
                registrations);
        }

        return registrations.Values.ToArray();
    }

    private static ProcessStepRegistration CreateRegistration(
        ProcessStepDefinition definition,
        IDictionary<Type, ProcessStepRegistration> registrations)
    {
        if (registrations.TryGetValue(
                definition.StepType,
                out var existing))
        {
            return existing;
        }

        //
        // Build relationships first.
        //
        var dependencies =
            definition.Dependencies
                .Select(x =>
                    CreateRegistration(
                        x,
                        registrations))
                .ToArray();

        var availableAfter =
            definition.AvailableAfter
                .Select(x =>
                    CreateRegistration(
                        x,
                        registrations))
                .ToArray();

        var availableUntil =
            definition.AvailableUntil
                .Select(x =>
                    CreateRegistration(
                        x,
                        registrations))
                .ToArray();

        var repeatable =
            GetRepeatableOptions(
                definition.StepType); 
        
        var registration =
            new ProcessStepRegistration(
                definition.StepType,
                definition.StepResultType,
                definition.HandlerType,
                dependencies,
                availableAfter,
                availableUntil,
                repeatable,
                definition.Metadata);

        registrations.Add(
            definition.StepType,
            registration);

        return registration;
    }

    private static RepeatableOptions GetRepeatableOptions(
        Type stepType)
    {
        return new RepeatableOptions
        {
            Enabled =
                stepType.IsDefined(
                    typeof(RepeatableAttribute),
                    inherit: false)
        };
    }

    private static void HydrateDefinition(
        ProcessStepDefinition definition,
        ProcessStepTypeDefinition typeDefinition,
        IReadOnlyDictionary<Type, ProcessStepDefinition> definitions)
    {
        foreach (var dependency in typeDefinition.Dependencies)
        {
            definition.Dependencies.Add(
                definitions[dependency]);
        }

        foreach (var availableAfter in typeDefinition.AvailableAfter)
        {
            definition.AvailableAfter.Add(
                definitions[availableAfter]);
        }

        foreach (var availableUntil in typeDefinition.AvailableUntil)
        {
            definition.AvailableUntil.Add(
                definitions[availableUntil]);
        }
    }

    private static Type GetHandlerType(
        IServiceCollection services,
        Type stepType)
    {
        var handlers =
            services
                .Where(x =>
                    x.ImplementationType is not null
                    && x.ImplementationType
                        .GetInterfaces()
                        .Any(i =>
                            i.IsGenericType
                            && i.GetGenericTypeDefinition() == typeof(IProcessStepHandler<,>)
                            && i.GetGenericArguments()[0] == stepType))
                .ToArray();

        if (handlers.Length == 0)
        {
            throw new InvalidOperationException(
                $"No process step handler registered for step '{stepType.FullName}'.");
        }

        if (handlers.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple process step handlers registered for step '{stepType.FullName}'.");
        }

        return handlers[0].ImplementationType!;
    }

    private static ProcessStepMetadata BuildStepMetadata(
        Type stepType)
    {
        var attribute =
            stepType.GetCustomAttribute<ProcessStepAttribute>()
            ?? throw new InvalidOperationException(
                $"Process step '{stepType.Name}' is missing ProcessStepAttribute.");

        return new ProcessStepMetadata(
            attribute.Name,
            attribute.Description,
            attribute.Version);
    }
}