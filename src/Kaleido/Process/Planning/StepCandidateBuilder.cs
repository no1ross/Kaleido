using System.Reflection;
using System.Text.Json;
using Kaleido.Process.Registry;

namespace Kaleido.Process.Planning;

internal interface IStepCandidateBuilder
{
    IReadOnlyCollection<StepCandidate> Build(ProcessorRequest request);
}

internal sealed class StepCandidateBuilder(
    IProcessStepRegistry registry)
    : IStepCandidateBuilder
{

    public IReadOnlyCollection<StepCandidate> Build(ProcessorRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var candidates = new List<StepCandidate>();

        foreach (var step in request.Steps)
        {
            var registration = registry.Find(step.Key);

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
            // If values is already a JsonElement (e.g. from HTTP deserialization), deserialize
            // directly to avoid a redundant Serialize → Deserialize round-trip.
            var instance =
                values is JsonElement je
                    ? je.Deserialize(stepType, SerializerOptions)
                    : JsonSerializer.Deserialize(
                        JsonSerializer.Serialize(values, SerializerOptions),
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
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
}

