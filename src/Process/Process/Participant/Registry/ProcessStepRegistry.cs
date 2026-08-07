using Kaleido.Process.Attributes;
using Kaleido.Process.Participant.Execution;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
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


    public IReadOnlyCollection<ProcessStepRegistration> InitialRegistrations =>
        _registrations
            .Where(x =>
                !x.Dependencies.Any() &&
                !x.AvailableAfter.Any())
            .ToArray();

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
        ArgumentNullException.ThrowIfNull(definitions);

        //
        // Pass 4a:
        // Build node graph from validated definitions.
        //
        var nodes =
            definitions.ToDictionary(
                x => x.StepType,
                x => new RegistrationNode
                {
                    Definition = x,
                    Repeatable = GetRepeatableOptions(
                        x.StepType)
                });

        //
        // Pass 4b:
        // Wire node relationships using direct lookup.
        //
        foreach (var node in nodes.Values)
        {
            node.Dependencies.AddRange(
                node.Definition.Dependencies
                    .Select(x => nodes[x.StepType]));

            node.AvailableAfter.AddRange(
                node.Definition.AvailableAfter
                    .Select(x => nodes[x.StepType]));

            node.AvailableUntil.AddRange(
                node.Definition.AvailableUntil
                    .Select(x => nodes[x.StepType]));
        }

        //
        // Pass 4c:
        // Create one registration slot per node.
        //
        // IMPORTANT:
        // This does not recursively create related registrations.
        // Each slot creates exactly one registration for exactly one node.
        //
        var slots =
            nodes.ToDictionary(
                x => x.Key,
                x => new RegistrationSlot(
                    x.Value));

        //
        // Pass 4d:
        // Wire each registration's immediate relationships.
        //
        // IMPORTANT:
        // This resolves direct references only.
        // It does not walk dependency chains.
        // It does not recursively materialize the graph.
        //
        foreach (var slot in slots.Values)
        {
            slot.Dependencies.AddRange(
                slot.Node.Dependencies
                    .Select(x =>
                        slots[x.Definition.StepType].Registration));

            slot.AvailableAfter.AddRange(
                slot.Node.AvailableAfter
                    .Select(x =>
                        slots[x.Definition.StepType].Registration));

            slot.AvailableUntil.AddRange(
                slot.Node.AvailableUntil
                    .Select(x =>
                        slots[x.Definition.StepType].Registration));
        }

        return definitions
            .Select(x => slots[x.StepType].Registration)
            .ToArray();
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
            attribute.Description ?? attribute.DisplayName ?? attribute.Name,
            attribute.Version,
            attribute.DisplayName ?? attribute.Name);
    }
}


internal sealed class RegistrationNode
{
    public required ProcessStepDefinition Definition
    {
        get;
        init;
    }

    public required RepeatableOptions Repeatable
    {
        get;
        init;
    }

    public List<RegistrationNode> Dependencies
    {
        get;
    } = [];

    public List<RegistrationNode> AvailableAfter
    {
        get;
    } = [];

    public List<RegistrationNode> AvailableUntil
    {
        get;
    } = [];
}

internal sealed class RegistrationSlot
{
    public RegistrationSlot(
        RegistrationNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        Node = node;

        Registration =
            new ProcessStepRegistration(
                node.Definition.StepType,
                node.Definition.StepResultType,
                node.Definition.HandlerType,
                new ReadOnlyCollection<ProcessStepRegistration>(
                    Dependencies),
                new ReadOnlyCollection<ProcessStepRegistration>(
                    AvailableAfter),
                new ReadOnlyCollection<ProcessStepRegistration>(
                    AvailableUntil),
                node.Repeatable,
                node.Definition.Metadata);
    }

    public RegistrationNode Node
    {
        get;
    }

    public ProcessStepRegistration Registration
    {
        get;
    }

    public List<ProcessStepRegistration> Dependencies
    {
        get;
    } = [];

    public List<ProcessStepRegistration> AvailableAfter
    {
        get;
    } = [];

    public List<ProcessStepRegistration> AvailableUntil
    {
        get;
    } = [];
}