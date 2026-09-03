namespace Kaleido.Process.Registry;

internal static class RegistrationValidator
{
    public static void Validate(
        IReadOnlyCollection<ProcessStepDefinition> definitions)
    {
        ValidateSelfReferences(
            definitions);

        ValidateCircularDependencies(
            definitions);
    }

    private static void ValidateSelfReferences(
        IReadOnlyCollection<ProcessStepDefinition> definitions)
    {
        foreach (var definition in definitions)
        {
            if (definition.Dependencies.Any(
                    x => x.StepType == definition.StepType))
            {
                throw new InvalidOperationException(
                    $"Process step '{definition.StepType.FullName}' cannot depend on itself.");
            }

            if (definition.AvailableAfter.Any(
                    x => x.StepType == definition.StepType))
            {
                throw new InvalidOperationException(
                    $"Process step '{definition.StepType.FullName}' cannot reference itself in AvailableAfter.");
            }

            if (definition.AvailableUntil.Any(
                    x => x.StepType == definition.StepType))
            {
                throw new InvalidOperationException(
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

            throw new InvalidOperationException(
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
}