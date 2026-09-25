using System.Reflection;

namespace Kaleido.Process.Registry;

internal interface IProcessStepRegistry
{
    IReadOnlyCollection<ProcessStepRegistration> Registrations { get; }

    IReadOnlyCollection<ProcessStepRegistration> InitialRegistrations { get; }

    ProcessStepRegistration? Find(string name);

    ProcessStepRegistration? Find(Type stepType);

    ProcessStepRegistration GetRegistration(string name);

    ProcessStepRegistration GetRegistration(Type stepType);
}

internal sealed partial class ProcessStepRegistry : IProcessStepRegistry
{
    private readonly IReadOnlyDictionary<string, ProcessStepRegistration> _byName;

    private readonly IReadOnlyDictionary<Type, ProcessStepRegistration> _byType;

    private readonly IReadOnlyCollection<ProcessStepRegistration> _registrations;

    private readonly IReadOnlyCollection<ProcessStepRegistration> _initialRegistrations;

    public ProcessStepRegistry(
        IEnumerable<Type> stepTypes,
        IReadOnlyDictionary<Type, Type> handlerTypes)
    {
        ArgumentNullException.ThrowIfNull(stepTypes);
        ArgumentNullException.ThrowIfNull(handlerTypes);

        var stepTypeArray =
            stepTypes
                .Distinct()
                .ToArray();

        // Pass 1
        var typeDefinitions =
            stepTypeArray
                .Select(stepType =>
                    BuildTypeDefinition(
                        handlerTypes,
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
        ValidateDefinitions(
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

        _initialRegistrations =
            registrations
                .Where(x =>
                    x.Dependencies.Count == 0 &&
                    x.AvailableAfter.Count == 0)
                .ToArray();
    }

    public IReadOnlyCollection<ProcessStepRegistration> Registrations =>
        _registrations;

    public IReadOnlyCollection<ProcessStepRegistration> InitialRegistrations =>
        _initialRegistrations;

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
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Process step '{name}' is not registered.");
    }

    public ProcessStepRegistration GetRegistration(Type stepType)
    {
        return Find(stepType)
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                $"Process step type '{stepType.FullName}' is not registered.");
    }

    private static ProcessStepTypeDefinition BuildTypeDefinition(
        IReadOnlyDictionary<Type, Type> handlerTypes,
        Type stepType)
    {
        if (!handlerTypes.TryGetValue(stepType, out var handlerType))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ProMissingHandler,
                $"No handler type registered for step '{stepType.FullName}'.");
        }

        var handlerInterface =
            handlerType
                .GetInterfaces()
                .Single(i => IsProcessStepHandler(i, stepType));

        var resultType =
            GetProcessStepResultType(
                handlerInterface);

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
            definition.AddDependency(dependency.DependsOnStep);
        }

        foreach (var availableAfter in
            stepType.GetCustomAttributes<AvailableAfterAttribute>())
        {
            definition.AddAvailableAfter(availableAfter.AvailableAfterStep);
        }

        foreach (var availableUntil in
            stepType.GetCustomAttributes<AvailableUntilAttribute>())
        {
            definition.AddAvailableUntil(availableUntil.AvailableUntilStep);
        }

        return definition;
    }

    private static Type? GetProcessStepResultType(
        Type handlerInterface)
    {
        var definition =
            handlerInterface.GetGenericTypeDefinition();

        if (definition == typeof(IProcessStepHandler<>))
        {
            return null;
        }

        if (definition == typeof(IProcessStepHandler<,>))
        {
            return handlerInterface.GenericTypeArguments[1];
        }

        throw new KaleidoConfigurationException(
            ConfigurationErrorCodes.ProInvalidHandler,
            $"Type '{handlerInterface.FullName}' is not a valid process step handler.");
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
            node.AddDependencies(
                node.Definition.Dependencies
                    .Select(x => nodes[x.StepType]));

            node.AddAvailableAfter(
                node.Definition.AvailableAfter
                    .Select(x => nodes[x.StepType]));

            node.AddAvailableUntil(
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
                    x.Value,
                    CreateGetResultFromTaskFunc(x.Value.Definition.HandlerType)));

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
            slot.AddDependencies(
                slot.Node.Dependencies
                    .Select(x =>
                        slots[x.Definition.StepType].Registration));

            slot.AddAvailableAfter(
                slot.Node.AvailableAfter
                    .Select(x =>
                        slots[x.Definition.StepType].Registration));

            slot.AddAvailableUntil(
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
            definition.AddDependency(definitions[dependency]);
        }

        foreach (var availableAfter in typeDefinition.AvailableAfter)
        {
            definition.AddAvailableAfter(definitions[availableAfter]);
        }

        foreach (var availableUntil in typeDefinition.AvailableUntil)
        {
            definition.AddAvailableUntil(definitions[availableUntil]);
        }
    }

    private static bool IsProcessStepHandler(
        Type interfaceType,
        Type stepType)
    {
        if (!interfaceType.IsGenericType)
        {
            return false;
        }

        var definition =
            interfaceType.GetGenericTypeDefinition();

        var genericArguments = interfaceType.GetGenericArguments();

        return
            (definition == typeof(IProcessStepHandler<>) ||
             definition == typeof(IProcessStepHandler<,>))
            &&
            genericArguments.Length > 0
            &&
            genericArguments[0] == stepType;
    }

    private static ProcessStepMetadata BuildStepMetadata(
        Type stepType)
    {
        var attribute =
            stepType.GetCustomAttribute<ProcessStepAttribute>()
            ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ProMissingAttribute,
                $"Process step '{stepType.Name}' is missing ProcessStepAttribute.");

        return new ProcessStepMetadata(
            attribute.Name,
            attribute.Description ?? attribute.DisplayName ?? attribute.Name,
            attribute.Version,
            attribute.DisplayName ?? attribute.Name);
    }
}

[ExcludeFromCodeCoverage]
internal sealed record ProcessStepDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    private readonly List<ProcessStepDefinition> _dependencies = [];
    private readonly List<ProcessStepDefinition> _availableAfter = [];
    private readonly List<ProcessStepDefinition> _availableUntil = [];

    public IReadOnlyCollection<ProcessStepDefinition> Dependencies => _dependencies;
    public IReadOnlyCollection<ProcessStepDefinition> AvailableAfter => _availableAfter;
    public IReadOnlyCollection<ProcessStepDefinition> AvailableUntil => _availableUntil;

    public void AddDependency(ProcessStepDefinition definition) => _dependencies.Add(definition);
    public void AddAvailableAfter(ProcessStepDefinition definition) => _availableAfter.Add(definition);
    public void AddAvailableUntil(ProcessStepDefinition definition) => _availableUntil.Add(definition);
}

[ExcludeFromCodeCoverage]
internal sealed record ProcessStepTypeDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    private readonly List<Type> _dependencies = [];
    private readonly List<Type> _availableAfter = [];
    private readonly List<Type> _availableUntil = [];

    public IReadOnlyCollection<Type> Dependencies => _dependencies;
    public IReadOnlyCollection<Type> AvailableAfter => _availableAfter;
    public IReadOnlyCollection<Type> AvailableUntil => _availableUntil;

    public void AddDependency(Type type) => _dependencies.Add(type);
    public void AddAvailableAfter(Type type) => _availableAfter.Add(type);
    public void AddAvailableUntil(Type type) => _availableUntil.Add(type);
}

[ExcludeFromCodeCoverage]
public sealed record ProcessStepDependencyGraph(
    IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> Dependencies,
    IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> Dependents);

[ExcludeFromCodeCoverage]
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

    private readonly List<RegistrationNode> _dependencies = [];
    private readonly List<RegistrationNode> _availableAfter = [];
    private readonly List<RegistrationNode> _availableUntil = [];

    public IReadOnlyCollection<RegistrationNode> Dependencies => _dependencies;
    public IReadOnlyCollection<RegistrationNode> AvailableAfter => _availableAfter;
    public IReadOnlyCollection<RegistrationNode> AvailableUntil => _availableUntil;

    public void AddDependencies(IEnumerable<RegistrationNode> nodes) => _dependencies.AddRange(nodes);
    public void AddAvailableAfter(IEnumerable<RegistrationNode> nodes) => _availableAfter.AddRange(nodes);
    public void AddAvailableUntil(IEnumerable<RegistrationNode> nodes) => _availableUntil.AddRange(nodes);
}

[ExcludeFromCodeCoverage]
internal sealed class RegistrationSlot
{
    private readonly List<ProcessStepRegistration> _dependencies = [];
    private readonly List<ProcessStepRegistration> _availableAfter = [];
    private readonly List<ProcessStepRegistration> _availableUntil = [];

    public RegistrationSlot(
        RegistrationNode node,
        Func<Task, IProcessStepHandlerResult>? getResultFromTask)
    {
        ArgumentNullException.ThrowIfNull(node);

        Node = node;

        Registration =
            new ProcessStepRegistration(
                node.Definition.StepType,
                node.Definition.StepResultType,
                node.Definition.HandlerType,
                _dependencies.AsReadOnly(),
                _availableAfter.AsReadOnly(),
                _availableUntil.AsReadOnly(),
                node.Repeatable,
                node.Definition.Metadata,
                getResultFromTask);
    }

    public RegistrationNode Node
    {
        get;
    }

    public ProcessStepRegistration Registration
    {
        get;
    }

    public IReadOnlyCollection<ProcessStepRegistration> Dependencies => _dependencies;
    public IReadOnlyCollection<ProcessStepRegistration> AvailableAfter => _availableAfter;
    public IReadOnlyCollection<ProcessStepRegistration> AvailableUntil => _availableUntil;

    public void AddDependencies(IEnumerable<ProcessStepRegistration> items) => _dependencies.AddRange(items);
    public void AddAvailableAfter(IEnumerable<ProcessStepRegistration> items) => _availableAfter.AddRange(items);
    public void AddAvailableUntil(IEnumerable<ProcessStepRegistration> items) => _availableUntil.AddRange(items);
}
