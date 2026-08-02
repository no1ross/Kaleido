using System.Reflection;
using System.Text.Json;

namespace Kaleido.Process.Participant.Planning;

internal class StepCandidateBuilder : IStepCandidateBuilder
{
    private readonly IProcessStepRegistry _registry;

    public StepCandidateBuilder(IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public IReadOnlyCollection<StepCandidate> Build(ParticipantRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidates = new List<StepCandidate>();

        foreach (var step in request.Steps)
        {
            var registration = _registry.Find(step.Key);

            if (registration is null)
            {
                candidates.Add(
                    StepCandidate.Invalid(
                        step.Key,
                        StepProcessingMessageCode.UnknownStep,
                        $"Process step '{step.Key}' is not registered."));

                continue;
            }

            var candidate =
                new StepCandidate
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
                    candidate.Status = StepCandidateStatus.Built;
                }
            }

            candidates.Add(candidate);
        }

        return candidates;
    }

    private static object? CreateStepInstance(
        StepCandidate candidate,
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
                candidate.MarkInvalid(
                        StepProcessingMessageCode.PropertyNotFound,
                        $"Property '{value.Key}' does not exist on process step '{stepType.Name}'.");

                continue;
            }

            if (!property.CanWrite)
            {
                candidate.MarkInvalid(
                        StepProcessingMessageCode.InvalidRequest,
                        $"Property '{property.Name}' on process step '{stepType.Name}' cannot be written.");

                continue;
            }

            var conversion =
                DataTypeMapper.TryConvertValue(
                    value.Value,
                    property.PropertyType);

            if (!conversion.Success)
            {
                candidate.MarkInvalid(
                        StepProcessingMessageCode.ConversionFailed,
                        $"Unable to convert value for property '{property.Name}' " +
                        $"on process step '{stepType.Name}' to type '{property.PropertyType.Name}'. " +
                        conversion.ErrorMessage);

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

    //private static object? CreateStepInstance(
    //    StepCandidate candidate,
    //    Type stepType,
    //    IReadOnlyDictionary<string, object?> values)
    //{
    //    try
    //    {
    //        var json =
    //            JsonSerializer.Serialize(values);

    //        var instance =
    //            JsonSerializer.Deserialize(
    //                json,
    //                stepType);

    //        if (instance is null)
    //        {
    //            candidate.MarkInvalid(
    //                StepProcessingMessageCode.InvalidRequest,
    //                $"Unable to create process step '{stepType.Name}'.");
    //        }

    //        return instance;
    //    }
    //    catch (JsonException exception)
    //    {
    //        candidate.MarkInvalid(
    //            StepProcessingMessageCode.InvalidRequest,
    //            $"Unable to create process step '{stepType.Name}'. {exception.Message}");

    //        return null;
    //    }
    //}
}

