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
        object? values)
    {
        try
        {
            var json =
                JsonSerializer.Serialize(
                    values,
                    SerializerOptions);

            var instance =
                JsonSerializer.Deserialize(
                    json,
                    stepType,
                    SerializerOptions);

            if (instance is null)
            {
                candidate.MarkInvalid(
                    StepProcessingMessageCode.InvalidRequest,
                    $"Unable to create process step '{stepType.Name}'.");
            }

            return instance;
        }
        catch (Exception exception) when (
            exception is JsonException ||
            exception is NotSupportedException)
        {
            candidate.MarkInvalid(
                StepProcessingMessageCode.InvalidRequest,
                $"Unable to create process step '{stepType.Name}'. {exception.Message}");

            return null;
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling =System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
}

