using Kaleido.Process.Attributes;
using Kaleido.Process.Participant.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido.Process.Participant.Steps;

internal sealed class ProcessStepRegistry : IProcessStepRegistry
{
    private readonly IReadOnlyDictionary<string, ProcessStepRegistration> _byName;

    private readonly IReadOnlyDictionary<Type, ProcessStepRegistration> _byType;

    private readonly IReadOnlyCollection<ProcessStepRegistration> _registrations;

    private readonly IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> _dependencies;

    private readonly IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> _dependents;

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

        var registrations =
            stepTypeArray
                .Select(stepType =>
                    BuildRegistration(
                        services,
                        stepType))
                .ToArray();

        _registrations = registrations;

        _byName =
            registrations.ToDictionary(
                x => x.Metadata.Name,
                StringComparer.OrdinalIgnoreCase);

        _byType =
            registrations.ToDictionary(
                x => x.StepType);

        _dependencies =
            BuildDependencies(
                stepTypeArray);

        ValidateDependencyGraph(
            stepTypeArray,
            _dependencies);

        _dependents =
            BuildDependents(
                stepTypeArray,
                _dependencies);

        Graph =
            new ProcessStepDependencyGraph(
                _dependencies,
                _dependents);
    }

    public IReadOnlyCollection<ProcessStepRegistration> Registrations =>
        _registrations;

    public ProcessStepDependencyGraph Graph { get; }

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

    public bool HasDependencies(Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        return _dependencies.TryGetValue(
                stepType,
                out var dependencies)
            && dependencies.Count > 0;
    }

    public bool HasDependents(Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        return _dependents.TryGetValue(
                stepType,
                out var dependents)
            && dependents.Count > 0;
    }

    public IReadOnlyCollection<ProcessStepRegistration> GetDependencies(
        Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        if (!_dependencies.TryGetValue(
                stepType,
                out var dependencies))
        {
            return [];
        }

        return dependencies
            .Select(GetRegistration)
            .ToArray();
    }

    public IReadOnlyCollection<ProcessStepRegistration> GetDependents(
        Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        if (!_dependents.TryGetValue(
                stepType,
                out var dependents))
        {
            return [];
        }

        return dependents
            .Select(GetRegistration)
            .ToArray();
    }

    public IReadOnlyCollection<ProcessStepRegistration> GetDependencyChain(
        Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        var visited =
            new HashSet<Type>();

        VisitDependencies(
            stepType,
            visited);

        return visited
            .Select(GetRegistration)
            .ToArray();
    }

    public IReadOnlyCollection<ProcessStepRegistration> GetDependentChain(
        Type stepType)
    {
        ArgumentNullException.ThrowIfNull(stepType);

        var visited =
            new HashSet<Type>();

        VisitDependents(
            stepType,
            visited);

        return visited
            .Select(GetRegistration)
            .ToArray();
    }

    private void VisitDependencies(
        Type stepType,
        HashSet<Type> visited)
    {
        if (!_dependencies.TryGetValue(
                stepType,
                out var dependencies))
        {
            return;
        }

        foreach (var dependency in dependencies)
        {
            if (visited.Add(dependency))
            {
                VisitDependencies(
                    dependency,
                    visited);
            }
        }
    }

    private void VisitDependents(
        Type stepType,
        HashSet<Type> visited)
    {
        if (!_dependents.TryGetValue(
                stepType,
                out var dependents))
        {
            return;
        }

        foreach (var dependent in dependents)
        {
            if (visited.Add(dependent))
            {
                VisitDependents(
                    dependent,
                    visited);
            }
        }
    }

    private static IReadOnlyDictionary<Type, IReadOnlyCollection<Type>>
        BuildDependencies(
            IReadOnlyCollection<Type> stepTypes)
    {
        return stepTypes.ToDictionary(
            stepType => stepType,
            stepType => (IReadOnlyCollection<Type>)
                stepType
                    .GetCustomAttributes<DependsOnStepAttribute>()
                    .Select(x => x.DependsOnStep)
                    .Distinct()
                    .ToArray());
    }

    private static IReadOnlyDictionary<Type, IReadOnlyCollection<Type>>
        BuildDependents(
            IReadOnlyCollection<Type> stepTypes,
            IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> dependencies)
    {
        var dependents =
            dependencies
                .SelectMany(x =>
                    x.Value.Select(dependency =>
                        new
                        {
                            Dependency = dependency,
                            Dependent = x.Key
                        }))
                .GroupBy(x => x.Dependency)
                .ToDictionary(
                    x => x.Key,
                    x => (IReadOnlyCollection<Type>)
                        x.Select(y => y.Dependent)
                            .Distinct()
                            .ToArray());

        return stepTypes.ToDictionary(
            stepType => stepType,
            stepType =>
                dependents.TryGetValue(
                    stepType,
                    out var values)
                    ? values
                    : []);
    }

    private static void ValidateDependencyGraph(
        IReadOnlyCollection<Type> stepTypes,
        IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> dependencies)
    {
        ValidateMissingDependencies(
            stepTypes,
            dependencies);

        ValidateSelfDependencies(
            dependencies);

        ValidateCircularDependencies(
            stepTypes,
            dependencies);
    }

    private static void ValidateMissingDependencies(
        IReadOnlyCollection<Type> stepTypes,
        IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> dependencies)
    {
        var registeredStepTypes =
            stepTypes.ToHashSet();

        foreach (var dependency in dependencies)
        {
            foreach (var requiredStepType in dependency.Value)
            {
                if (!registeredStepTypes.Contains(requiredStepType))
                {
                    throw new InvalidOperationException(
                        $"Process step '{dependency.Key.FullName}' depends on " +
                        $"'{requiredStepType.FullName}', but that step is not registered.");
                }
            }
        }
    }

    private static void ValidateSelfDependencies(
        IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> dependencies)
    {
        foreach (var dependency in dependencies)
        {
            if (dependency.Value.Contains(dependency.Key))
            {
                throw new InvalidOperationException(
                    $"Process step '{dependency.Key.FullName}' cannot depend on itself.");
            }
        }
    }

    private static void ValidateCircularDependencies(
        IReadOnlyCollection<Type> stepTypes,
        IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> dependencies)
    {
        foreach (var stepType in stepTypes)
        {
            ValidateCircularDependency(
                stepType,
                dependencies,
                new HashSet<Type>(),
                new Stack<Type>());
        }
    }

    private static void ValidateCircularDependency(
        Type stepType,
        IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> dependencies,
        HashSet<Type> visited,
        Stack<Type> path)
    {
        if (path.Contains(stepType))
        {
            var cycle =
                path
                    .Reverse()
                    .Append(stepType)
                    .SkipWhile(x => x != stepType)
                    .Select(x => x.Name)
                    .ToArray();

            throw new InvalidOperationException(
                $"Circular process step dependency detected: {string.Join(" -> ", cycle)}");
        }

        if (!visited.Add(stepType))
        {
            return;
        }

        path.Push(stepType);

        if (dependencies.TryGetValue(
                stepType,
                out var requiredStepTypes))
        {
            foreach (var requiredStepType in requiredStepTypes)
            {
                ValidateCircularDependency(
                    requiredStepType,
                    dependencies,
                    visited,
                    path);
            }
        }

        path.Pop();
    }

    private static ProcessStepRegistration BuildRegistration(
        IServiceCollection services,
        Type stepType)
    {
        var handlerType =
            GetHandlerType(
                services,
                stepType);

        var metadata =
            BuildStepMetadata(
                stepType);

        return new ProcessStepRegistration(
            stepType,
            handlerType,
            metadata);
    }

    private static Type GetHandlerType(
        IServiceCollection services,
        Type stepType)
    {
        var handlerInterface =
            typeof(IProcessStepHandler<>)
                .MakeGenericType(stepType);

        var handlers =
            services
                .Where(x => x.ServiceType == handlerInterface)
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

        return handlers[0].ImplementationType
            ?? throw new InvalidOperationException(
                $"Process step handler registration '{handlerInterface.Name}' is missing an implementation type.");
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
            attribute.Description);
    }
}