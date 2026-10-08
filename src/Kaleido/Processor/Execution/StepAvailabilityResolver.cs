using Kaleido.Processor.Context;
using Kaleido.Processor.Registry;

namespace Kaleido.Processor.Execution;

internal interface IStepAvailabilityResolver
{
    IReadOnlyCollection<string> Resolve(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context);

    /// <summary>
    /// The steps that may run now, before anything in the current request executes: the
    /// process rules applied to the steps already completed.
    /// </summary>
    IReadOnlyCollection<string> ResolveCurrent(
        ProcessorContext context);
}

internal sealed class StepAvailabilityResolver(
    IProcessorStepRegistry registry)
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

        var completedSteps =
            GetCompletedStepNames(
                context);

        completedSteps.Add(
            currentCandidate.StepName);

        return Resolve(
            completedSteps);
    }

    public IReadOnlyCollection<string> ResolveCurrent(
        ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Resolve(
            GetCompletedStepNames(
                context));
    }

    private IReadOnlyCollection<string> Resolve(
        IReadOnlySet<string> completedSteps)
    {
        var registrations = registry.Registrations;

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

    private static HashSet<string> GetCompletedStepNames(
        ProcessorContext context)
    {
        return context.Steps
            .Where(x =>
                x.Status == StepExecutionStatus.Completed)
            .Select(x =>
                x.StepName)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);
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
