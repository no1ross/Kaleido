using System.Reflection;
using Kaleido.Process.Execution;

namespace Kaleido.Process.Registry;

internal sealed partial class ProcessStepRegistry
{
    private static void ValidateDefinitions(
        IReadOnlyCollection<ProcessStepDefinition> definitions)
    {
        ValidateSelfReferences(definitions);
        ValidateCircularDependencies(definitions);
    }

    private static void ValidateSelfReferences(
        IReadOnlyCollection<ProcessStepDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (definition.Dependencies.Any(
                    x => x.StepType == definition.StepType))
            {
                throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.ProInvalidRegistration,
                    $"Process step '{definition.StepType.FullName}' cannot depend on itself.");
            }

            if (definition.AvailableAfter.Any(
                    x => x.StepType == definition.StepType))
            {
                throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.ProInvalidRegistration,
                    $"Process step '{definition.StepType.FullName}' cannot reference itself in AvailableAfter.");
            }

            if (definition.AvailableUntil.Any(
                    x => x.StepType == definition.StepType))
            {
                throw new KaleidoConfigurationException(
                    ConfigurationErrorCodes.ProInvalidRegistration,
                    $"Process step '{definition.StepType.FullName}' cannot reference itself in AvailableUntil.");
            }
        }
    }

    private static void ValidateCircularDependencies(
        IReadOnlyCollection<ProcessStepDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            ValidateCircularDependency(
                definition,
                new HashSet<Type>(),
                new Stack<Type>());
        }
    }

    private static void ValidateCircularDependency(
        ProcessStepDefinition definition,
        HashSet<Type> visited,
        Stack<Type> path)
    {
        if (path.Contains(
                definition.StepType))
        {
            var cycle =
                path.Reverse()
                    .Append(definition.StepType)
                    .SkipWhile(x => x != definition.StepType)
                    .Select(x => x.Name);

            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ProInvalidRegistration,
                $"Circular process step dependency detected: {string.Join(" -> ", cycle)}");
        }

        if (!visited.Add(
                definition.StepType))
        {
            return;
        }

        path.Push(
            definition.StepType);

        foreach (var dependency in definition.Dependencies)
        {
            ValidateCircularDependency(
                dependency,
                visited,
                path);
        }

        path.Pop();
    }

    private static Func<Task, IProcessStepHandlerResult>? CreateGetResultFromTaskFunc(
        Type handlerType)
    {
        var executeAsyncMethod =
            handlerType.GetMethod(
                nameof(IProcessStepHandler<object>.ExecuteAsync),
                BindingFlags.Public | BindingFlags.Instance);

        if (executeAsyncMethod is null)
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ProInvalidHandler,
                $"Handler '{handlerType.FullName}' does not expose ExecuteAsync.");
        }

        var taskType = executeAsyncMethod.ReturnType;
        var resultProperty = taskType.GetProperty(nameof(Task<object>.Result))
            ?? throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.ProInvalidHandler,
                $"Task type '{taskType.FullName}' does not have a Result property.");

        return task =>
        {
            var result = resultProperty.GetValue(task);
            if (result is IProcessStepHandlerResult handlerResult)
            {
                return handlerResult;
            }

            throw new KaleidoFrameworkException(
                FrameworkErrorCodes.InvalidHandlerResult,
                $"Handler returned an invalid handler result of type '{result?.GetType().FullName}'.");
        };
    }
}
