using System.Collections.Frozen;
using System.Reflection;
using Kaleido.Registry;

namespace Kaleido.Processor.Registry;

/// <summary>
/// Read-only view of all process steps registered for this service.
/// </summary>
/// <remarks>
/// Populated at startup by <c>AddProcessor()</c> via assembly scanning for types
/// implementing <see cref="IProcessStep"/> (each described by its required
/// <see cref="ProcessStepAttribute"/>). The registry is immutable after the DI
/// container is built.
/// Inject this interface to inspect available steps, resolve step metadata by name
/// (the step type's name) or CLR type, or drive dynamic process execution logic.
/// </remarks>
internal interface IProcessorStepRegistry
{
    /// <summary>
    /// All registered process steps, in an unspecified order.
    /// </summary>
    IReadOnlyCollection<ProcessStepRegistration> Registrations { get; }

    /// <summary>
    /// The subset of registered steps that have no <c>DependsOn</c> or
    /// <c>AvailableAfter</c> constraints — i.e., the steps that are valid
    /// entry points for a new process execution. Information steps are never
    /// initial (they need a pending information request).
    /// </summary>
    IReadOnlyCollection<ProcessStepRegistration> InitialRegistrations { get; }

    /// <summary>
    /// Returns the registration for the step with the given name, or
    /// <see langword="null"/> if no step with that name is registered.
    /// Name comparison is case-insensitive.
    /// </summary>
    ProcessStepRegistration? Find(string name);

    /// <summary>
    /// Returns the registration for the step whose CLR type matches
    /// <paramref name="stepType"/>, or <see langword="null"/> if not found.
    /// </summary>
    ProcessStepRegistration? Find(Type stepType);

    /// <summary>
    /// Returns the registration for the step with the given name.
    /// </summary>
    /// <exception cref="KaleidoFrameworkException">
    /// Thrown when no step with <paramref name="name"/> is registered.
    /// </exception>
    ProcessStepRegistration GetRegistration(string name);

    /// <summary>
    /// Returns the registration for the step whose CLR type matches
    /// <paramref name="stepType"/>.
    /// </summary>
    /// <exception cref="KaleidoFrameworkException">
    /// Thrown when no step matching <paramref name="stepType"/> is registered.
    /// </exception>
    ProcessStepRegistration GetRegistration(Type stepType);
}

internal sealed partial class ProcessorStepRegistry : IProcessorStepRegistry
{
    private readonly FrozenDictionary<string, ProcessStepRegistration> _byName;

    private readonly FrozenDictionary<Type, ProcessStepRegistration> _byType;

    private readonly IReadOnlyCollection<ProcessStepRegistration> _registrations;

    private readonly IReadOnlyCollection<ProcessStepRegistration> _initialRegistrations;

    public ProcessorStepRegistry(
        IEnumerable<Type> stepTypes,
        IReadOnlyDictionary<Type, Type> handlerTypes)
        : this(stepTypes, handlerTypes, AuthorizationMetadata.Unspecified)
    {
    }

    public ProcessorStepRegistry(
        IEnumerable<Type> stepTypes,
        IReadOnlyDictionary<Type, Type> handlerTypes,
        AuthorizationMetadata defaultAuthorization)
    {
        ArgumentNullException.ThrowIfNull(stepTypes);
        ArgumentNullException.ThrowIfNull(handlerTypes);
        ArgumentNullException.ThrowIfNull(defaultAuthorization);

        // Materialize one definition per step — relationship attributes are
        // collected as Type references (all steps may not exist yet).
        var definitions =
            stepTypes
                .Distinct()
                .Select(stepType => BuildDefinition(handlerTypes, stepType, defaultAuthorization))
                .ToArray();

        var definitionsByType = definitions.ToDictionary(x => x.StepType);

        // Hydrate the Type references into definition references — requires
        // every definition to exist first.
        foreach (var definition in definitions)
        {
            HydrateDefinition(definition, definitionsByType);
        }

        ValidateDefinitions(definitions);

        var registrations = BuildRegistrations(definitions);

        _registrations = registrations;
        _byName = registrations.ToFrozenDictionary(x => x.Metadata.Name, StringComparer.OrdinalIgnoreCase);
        _byType = registrations.ToFrozenDictionary(x => x.StepType);

        // An information step can never start a process: it is only reachable
        // as the required step, with a pending information request.
        _initialRegistrations =
            registrations
                .Where(x =>
                    !x.IsInformationStep &&
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

    private static ProcessStepDefinition BuildDefinition(
        IReadOnlyDictionary<Type, Type> handlerTypes,
        Type stepType,
        AuthorizationMetadata defaultAuthorization)
    {
        if (!handlerTypes.TryGetValue(stepType, out var handlerType))
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.MissingHandler,
                $"No handler type registered for step '{stepType.FullName}'.");
        }

        var handlerInterface =
            handlerType
                .GetGenericInterfacesFor(
                    stepType,
                    typeof(IProcessStepHandler<>),
                    typeof(IProcessStepHandler<,>))
                .Single();

        var resultType =
            GetProcessStepResultType(
                handlerInterface);

        var metadata =
            BuildStepMetadata(
                stepType,
                defaultAuthorization);

        var definition =
            new ProcessStepDefinition
            {
                StepType = stepType,
                StepResultType = resultType,
                HandlerType = handlerType,
                Metadata = metadata,
            };

        foreach (var dependency in
            GetRelationships(stepType, typeof(DependsOnAttribute<>)))
        {
            definition.AddDependencyType(dependency);
        }

        foreach (var availableAfter in
            GetRelationships(stepType, typeof(AvailableAfterAttribute<>)))
        {
            definition.AddAvailableAfterType(availableAfter);
        }

        foreach (var availableUntil in
            GetRelationships(stepType, typeof(AvailableUntilAttribute<>)))
        {
            definition.AddAvailableUntilType(availableUntil);
        }

        return definition;
    }

    private static IEnumerable<Type> GetRelationships(
        Type stepType,
        Type attributeDefinition)
    {
        return stepType
            .GetCustomAttributes(inherit: false)
            .Where(x =>
                x.GetType().IsGenericType &&
                x.GetType().GetGenericTypeDefinition() == attributeDefinition)
            .OfType<IStepRelationshipAttribute>()
            .Select(x => x.StepType);
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
            ProcessorErrorCodes.InvalidHandler,
            $"Type '{handlerInterface.FullName}' is not a valid process step handler.");
    }

    // Create one registration slot per definition, then wire each slot's
    // direct relationships by lookup. Two phases are required: a registration
    // can only reference slots that already exist.
    private static ProcessStepRegistration[] BuildRegistrations(
        IReadOnlyCollection<ProcessStepDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var slots =
            definitions.ToDictionary(
                x => x.StepType,
                x => new RegistrationSlot(
                    x,
                    GetRepeatableOptions(x.StepType),
                    CreateGetResultFromTaskFunc(x.HandlerType),
                    CreateInvokeHandlerAsyncFunc(x.HandlerType)));

        foreach (var slot in slots.Values)
        {
            slot.AddDependencies(
                slot.Definition.Dependencies
                    .Select(x =>
                        slots[x.StepType].Registration));

            slot.AddAvailableAfter(
                slot.Definition.AvailableAfter
                    .Select(x =>
                        slots[x.StepType].Registration));

            slot.AddAvailableUntil(
                slot.Definition.AvailableUntil
                    .Select(x =>
                        slots[x.StepType].Registration));
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
                    inherit: false),
        };
    }

    private static void HydrateDefinition(
        ProcessStepDefinition definition,
        IReadOnlyDictionary<Type, ProcessStepDefinition> definitions)
    {
        foreach (var dependency in definition.DependencyTypes)
        {
            definition.AddDependency(Target(definition, dependency, "DependsOn", definitions));
        }

        foreach (var availableAfter in definition.AvailableAfterTypes)
        {
            definition.AddAvailableAfter(Target(definition, availableAfter, "AvailableAfter", definitions));
        }

        foreach (var availableUntil in definition.AvailableUntilTypes)
        {
            definition.AddAvailableUntil(Target(definition, availableUntil, "AvailableUntil", definitions));
        }
    }

    // A relationship may name a step this processor does not register (its assembly is not scanned,
    // or TypeFilter excludes it): a clear startup error instead of a KeyNotFoundException.
    private static ProcessStepDefinition Target(
        ProcessStepDefinition definition,
        Type target,
        string relationship,
        IReadOnlyDictionary<Type, ProcessStepDefinition> definitions) =>
        definitions.TryGetValue(target, out var found)
            ? found
            : throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                $"Step '{definition.StepType.FullName}' declares [{relationship}<{target.Name}>], but '{target.FullName}' is not a registered process step of this processor. "
                + "Register its assembly, or check that TypeFilter does not exclude it.");

    private static ProcessStepMetadata BuildStepMetadata(
        Type stepType,
        AuthorizationMetadata defaultAuthorization)
    {
        var attribute =
            stepType.GetCustomAttribute<ProcessStepAttribute>()
            ?? throw new KaleidoConfigurationException(
                ProcessorErrorCodes.MissingAttribute,
                $"Process step '{stepType.Name}' is missing ProcessStepAttribute.");

        return new ProcessStepMetadata
        {
            Name = stepType.Name,
            Description = attribute.Description,
            Version = attribute.Version,
            DisplayName = attribute.DisplayName,
            Authorization = AuthorizationMetadata.ForType(stepType, defaultAuthorization),
            Destructive = attribute.Destructive,
            External = attribute.External,
            RequiresConfirmation = attribute.RequiresConfirmation,
        };
    }
}
