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

internal sealed class StepAvailabilityResolver
    : IStepAvailabilityResolver
{
    private readonly IProcessStepRegistry _registry;

    public StepAvailabilityResolver(
        IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public IReadOnlyCollection<string> Resolve(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ProcessorContext context)
    {
        ArgumentNullException.ThrowIfNull(currentCandidate);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

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
                x.Metadata.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    //private IReadOnlyCollection<ProcessStepRegistration> GetScopedRegistrations(
    //    StepCandidate currentCandidate,
    //    IReadOnlyCollection<StepCandidate> candidates,
    //    ProcessorContext context)
    //{
    //    var registrations =
    //        new Dictionary<Type, ProcessStepRegistration>();

    //    if (currentCandidate.Registration is not null)
    //    {
    //        registrations.TryAdd(
    //            currentCandidate.Registration.StepType,
    //            currentCandidate.Registration);
    //    }

    //    foreach (var candidate in candidates)
    //    {
    //        if (candidate.Registration is not null)
    //        {
    //            registrations.TryAdd(
    //                candidate.Registration.StepType,
    //                candidate.Registration);
    //        }
    //    }

    //    foreach (var completedStep in context.Steps.Where(
    //                 x => x.Status == StepExecutionStatus.Completed))
    //    {
    //        var registration =
    //            _registry.Find(
    //                completedStep.StepName);

    //        if (registration is not null)
    //        {
    //            registrations.TryAdd(
    //                registration.StepType,
    //                registration);
    //        }
    //    }

    //    return registrations.Values.ToArray();
    //}

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