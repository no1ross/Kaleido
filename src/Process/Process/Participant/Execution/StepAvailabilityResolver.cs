using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Planning;
using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.Participant.Execution;

internal interface IStepAvailabilityResolver
{
    IReadOnlyCollection<string> Resolve(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context);
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
        ParticipantContext context)
    {
        ArgumentNullException.ThrowIfNull(currentCandidate);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        var registrations =
            GetScopedRegistrations(
                currentCandidate,
                candidates,
                context);

        var completedSteps =
            GetCompletedStepNames(
                currentCandidate,
                context);

        return registrations
            .Where(x =>
                !completedSteps.Contains(
                    x.Metadata.Name))
            .Where(x =>
                DependenciesSatisfied(
                    x,
                    completedSteps))
            .Where(x =>
                AvailableAfterSatisfied(
                    x,
                    completedSteps))
            .Where(x =>
                AvailableUntilSatisfied(
                    x,
                    completedSteps))
            .Select(x =>
                x.Metadata.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyCollection<ProcessStepRegistration> GetScopedRegistrations(
        StepCandidate currentCandidate,
        IReadOnlyCollection<StepCandidate> candidates,
        ParticipantContext context)
    {
        var registrations =
            new Dictionary<Type, ProcessStepRegistration>();

        if (currentCandidate.Registration is not null)
        {
            registrations.TryAdd(
                currentCandidate.Registration.StepType,
                currentCandidate.Registration);
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Registration is not null)
            {
                registrations.TryAdd(
                    candidate.Registration.StepType,
                    candidate.Registration);
            }
        }

        foreach (var completedStep in context.Steps.Where(
                     x => x.Status == StepExecutionStatus.Completed))
        {
            var registration =
                _registry.Find(
                    completedStep.StepName);

            if (registration is not null)
            {
                registrations.TryAdd(
                    registration.StepType,
                    registration);
            }
        }

        return registrations.Values.ToArray();
    }

    private IReadOnlySet<string> GetCompletedStepNames(
        StepCandidate currentCandidate,
        ParticipantContext context)
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