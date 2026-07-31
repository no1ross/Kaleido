using System.Reflection;

namespace Kaleido.Process.Participant.Execution;

internal static class ExecutionCandidateBuilder
{
    public static ExecutionCandidateBuilderResult Build(
        ParticipantRequest request,
        IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registry);

        var candidates =
            new List<ExecutionCandidate>();

        foreach (var step in request.Steps)
        {
            var registration =
                registry.Find(step.Key);

            if (registration is null)
            {
                candidates.Add(
                    ExecutionCandidate.Invalid(
                        step.Key,
                        ProcessStepMessage.Warning(
                            ProcessStepMessageCode.UnknownStep,
                            $"Process step '{step.Key}' is not registered.")));

                continue;
            }

            var candidate =
                new ExecutionCandidate
                {
                    StepName = step.Key,
                    Registration = registration
                };

            var instance =
                CreateStepInstance(
                    candidate,
                    registration.StepType,
                    step.Value);

            if (instance is not null)
            {
                candidate.Step = instance;

                if (!candidate.HasErrors)
                {
                    candidate.Status =
                        ExecutionCandidateStatus.Built;
                }
            }

            candidates.Add(candidate);
        }

        return new ExecutionCandidateBuilderResult
        {
            Candidates = candidates
        };
    }

    private static object? CreateStepInstance(
        ExecutionCandidate candidate,
        Type stepType,
        IReadOnlyDictionary<string, object?> values)
    {
        var instance =
            Activator.CreateInstance(stepType)
            ?? throw new InvalidOperationException(
                $"Unable to create instance of '{stepType.FullName}'. " +
                "Process steps must be constructable by the framework.");

        foreach (var value in values)
        {
            var property =
                stepType.GetProperty(
                    value.Key,
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase);

            if (property is null)
            {
                candidate.Status =
                    ExecutionCandidateStatus.Invalid;

                candidate.AddMessage(
                    ProcessStepMessage.Error(
                        ProcessStepMessageCode.PropertyNotFound,
                        $"Property '{value.Key}' does not exist on process step '{stepType.Name}'."));

                continue;
            }

            if (!property.CanWrite)
            {
                candidate.Status =
                    ExecutionCandidateStatus.Invalid;

                candidate.AddMessage(
                    ProcessStepMessage.Error(
                        ProcessStepMessageCode.InvalidRequest,
                        $"Property '{property.Name}' on process step '{stepType.Name}' cannot be written."));

                continue;
            }

            var conversion =
                DataTypeMapper.TryConvertValue(
                    value.Value,
                    property.PropertyType);

            if (!conversion.Success)
            {
                candidate.Status =
                    ExecutionCandidateStatus.Invalid;

                candidate.AddMessage(
                    ProcessStepMessage.Error(
                        ProcessStepMessageCode.ConversionFailed,
                        $"Unable to convert value for property '{property.Name}' " +
                        $"on process step '{stepType.Name}' to type '{property.PropertyType.Name}'. " +
                        conversion.ErrorMessage));

                continue;
            }

            property.SetValue(
                instance,
                conversion.Value);
        }

        if (candidate.HasErrors)
        {
            return null;
        }

        return instance;
    }
}

