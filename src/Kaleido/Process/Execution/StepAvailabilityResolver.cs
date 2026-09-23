using Kaleido.Process.Context;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;

namespace Kaleido.Process.Execution;

internal interface IStepAvailabilityResolver
{
    IReadOnlyCollection<string> Resolve(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context);
}

internal sealed class StepAvailabilityResolver(
    IProcessStepRegistry registry)
    : IStepAvailabilityResolver
{

    public IReadOnlyCollection<string> Resolve(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(currentCandidate);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        var registrations = registry.Registrations;

        var completedSteps =
            GetCompletedStepNames(
                currentCandidate,
                context);

        var filtered =
            registrations
                .Where(x =>
                    x.Repeatable.Enabled ||
                    !completedSteps.Contains(
                        x.Metadata.Name))
                .ToArray();

        var dependenciesSatisfied =
            filtered
                .Where(x =>
                    DependenciesSatisfied(
                        x,
                        completedSteps))
                .ToArray();

        var availableAfterSatisfied =
            dependenciesSatisfied
                .Where(x =>
                    AvailableAfterSatisfied(
                        x,
                        completedSteps))
                .ToArray();

        var availableUntilSatisfied =
            availableAfterSatisfied
                .Where(x =>
                    AvailableUntilSatisfied(
                        x,
                        completedSteps))
                .ToArray();

        return availableUntilSatisfied
            .Select(x => x.Metadata.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlySet<string> GetCompletedStepNames(
        StepCandidate currentCandidate,
        ProcessorContext context)
    {
        var completedSteps =
            context.Steps
                .Where(x =>
                    x.Status == StepExecutionStatus.Completed)
                .Select(x =>
                    x.StepName)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        completedSteps.Add(
            currentCandidate.StepName);

        return completedSteps;
    }

    private static bool DependenciesSatisfied(
        ProcessStepRegistration registration,
        IReadOnlySet<string> completedSteps)
    {
        return registration.Dependencies.All(
            x => completedSteps.Contains(
                x.Metadata.Name));
    }

    private static bool AvailableAfterSatisfied(
        ProcessStepRegistration registration,
        IReadOnlySet<string> completedSteps)
    {
        return registration.AvailableAfter.All(
            x => completedSteps.Contains(
                x.Metadata.Name));
    }

    private static bool AvailableUntilSatisfied(
        ProcessStepRegistration registration,
        IReadOnlySet<string> completedSteps)
    {
        return registration.AvailableUntil.All(
            x => !completedSteps.Contains(
                x.Metadata.Name));
    }
}
