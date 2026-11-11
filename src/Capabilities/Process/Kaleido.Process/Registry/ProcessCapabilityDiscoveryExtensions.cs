using System.Reflection;

namespace Kaleido.Process.Registry;

internal static class ProcessCapabilityDiscoveryExtensions
{
    /// <summary>Validates the scanned process steps and resolves their handlers without registering services.</summary>
    internal static (Type[] StepTypes, Dictionary<Type, Type> HandlerTypes) DiscoverProcessCapabilities(
        this IEnumerable<Type> types,
        Func<Type, bool>? typeFilter)
    {
        var scanned = types.ToArray();
        var candidateTypes =
            scanned
                .Where(x =>
                    (IsProcessStepType(x) ||
                    Attribute.IsDefined(x, typeof(ProcessStepAttribute))) && x.PassesTypeFilter(
                        typeFilter,
                        ProcessorErrorCodes.InvalidRegistration,
                        "process step"))
                .ToArray();

        ValidateAttributesHaveInterface(candidateTypes);

        var stepTypes = candidateTypes.Where(IsProcessStepType).ToArray();
        ValidateProcessSteps(stepTypes);
        var handlerTypes = stepTypes.ToDictionary(x => x, x => FindHandler(x, scanned));

        return (stepTypes, handlerTypes);
    }

    private static void ValidateProcessSteps(Type[] stepTypes)
    {
        if (stepTypes.Length == 0)
        {
            // No process steps to register - this is valid for Queryable-only services
            return;
        }

        foreach (var stepType in stepTypes)
        {
            var metadata = GetProcessStepMetadata(stepType);

            RequireNonEmpty(stepType, metadata.DisplayName, nameof(ProcessStepAttribute.DisplayName));
            RequireNonEmpty(stepType, metadata.Description, nameof(ProcessStepAttribute.Description));
            RequireInformationStepShape(stepType);
        }

        var duplicateNames =
            stepTypes
                .Select(x => new
                {
                    StepType = x,
                    x.Name,
                })
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Skip(1).Any())
                .ToArray();

        if (duplicateNames.Length == 0)
        {
            return;
        }

        var duplicateDetails =
            string.Join(
                Environment.NewLine,
                duplicateNames.Select(x =>
                {
                    var stepTypesForName =
                        string.Join(", ", x.Select(y => y.StepType.FullName));

                    return $"Name '{x.Key}' is used by: {stepTypesForName}";
                }));

        throw new KaleidoConfigurationException(
            ProcessorErrorCodes.DuplicateStep,
            $"Duplicate process step names were found.{Environment.NewLine}{duplicateDetails}");
    }

    // An information step's payload is the answers to its pending request and nothing else:
    // known inputs belong on ordinary steps, so the same data never lives in two places.
    private static void RequireInformationStepShape(Type stepType)
    {
        if (!typeof(IInformationStep).IsAssignableFrom(stepType))
        {
            return;
        }

        var extra =
            stepType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(x =>
                    x.Name is not (nameof(IInformationStep.InformationRequestId) or nameof(IInformationStep.ExchangeId) or nameof(IInformationStep.Items)))
                .Select(x => x.Name)
                .ToArray();

        if (extra.Length > 0)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                $"Information step '{stepType.FullName}' declares {string.Join(", ", extra)}. "
                + $"An {nameof(IInformationStep)} may only declare {nameof(IInformationStep.InformationRequestId)}, {nameof(IInformationStep.ExchangeId)} and {nameof(IInformationStep.Items)}; "
                + "put known inputs on an ordinary step instead.");
        }
    }

    private static bool IsProcessStepType(Type type) =>
        type is { IsClass: true, IsAbstract: false }
        && typeof(IProcessStep).IsAssignableFrom(type);

    // [ProcessStep] on a type that is no IProcessStep would otherwise be silently ignored. The
    // analyzer (KAL2007) reports it at compile time, but analyzers can be switched off:
    // registration enforces it at startup, on purpose (#204).
    private static void ValidateAttributesHaveInterface(IEnumerable<Type> types)
    {
        var invalid =
            types
                .Where(x =>
                    Attribute.IsDefined(x, typeof(ProcessStepAttribute)) &&
                    !typeof(IProcessStep).IsAssignableFrom(x))
                .Select(x => x.FullName)
                .ToArray();

        if (invalid.Length > 0)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidRegistration,
                $"[ProcessStep] is applied to types that do not implement {nameof(IProcessStep)}: {string.Join(", ", invalid)}. "
                + $"A process step is identified by {nameof(IProcessStep)}; the attribute only describes it.");
        }
    }

    private static void RequireNonEmpty(Type stepType, string? value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.MissingAttribute,
                $"Process step '{stepType.FullName}' must specify a non-empty {propertyName} on [ProcessStep].");
        }
    }

    private static ProcessStepAttribute GetProcessStepMetadata(Type stepType)
    {
        var metadata = stepType.GetCustomAttribute<ProcessStepAttribute>();

        if (metadata is null)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.MissingAttribute,
                $"Process step '{stepType.FullName}' implements {nameof(IProcessStep)} but is missing the required [ProcessStep] attribute.");
        }

        return metadata;
    }

    private static Type FindHandler(Type stepType, IEnumerable<Type> types)
    {
        var handlerTypes =
            types
                .Where(type =>
                    type.ImplementsGenericInterfaceFor(
                        stepType,
                        typeof(IProcessStepHandler<>),
                        typeof(IProcessStepHandler<,>)))
                .ToArray();

        if (handlerTypes.Length == 0)
        {
            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.MissingHandler,
                $"Process step '{stepType.Name}' ({stepType.FullName}) does not have a registered handler.");
        }

        if (handlerTypes.Length > 1)
        {
            var handlers = string.Join(", ", handlerTypes.Select(x => x.FullName));

            throw new KaleidoConfigurationException(
                ProcessorErrorCodes.InvalidHandler,
                $"Process step '{stepType.Name}' ({stepType.FullName}) has multiple handlers: {handlers}.");
        }

        return handlerTypes[0];
    }
}
