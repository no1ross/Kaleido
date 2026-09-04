using Kaleido.Process.Context;
using Kaleido.Process.Planning;
using Kaleido.Process.Registry;

namespace Kaleido.Process.Execution;

internal interface IStepAvailabilityResolver
{
    IReadOnlyCollection<ProcessStepReference> Resolve(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context);
}

internal sealed class StepAvailabilityResolver
    : IStepAvailabilityResolver
{
    private readonly IProcessStepRegistry _registry;
    private readonly IProcessorRegistry _processorRegistry;

    public StepAvailabilityResolver(
        IProcessStepRegistry registry,
        IProcessorRegistry processorRegistry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(processorRegistry);

        _registry = registry;
        _processorRegistry = processorRegistry;
    }

    public IReadOnlyCollection<ProcessStepReference> Resolve(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(currentCandidate);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        var processorName =
            _processorRegistry.Registrations
                .Single()
                .Name;

        var registrations = _registry.Registrations;

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
            .Select(x =>
                new ProcessStepReference
                {
                    ProcessorName = processorName,
                    StepName = x.Metadata.Name
                })
            .DistinctBy(x => x.StepName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlySet<string> GetCompletedStepNames(
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
