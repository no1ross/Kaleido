
using Kaleido.Exceptions;

namespace Kaleido.Process.Participant;

public interface IParticipantRuntime
{
    Task<ProcessResult> ProcessAsync(
        ParticipantRequest request,
        ParticipantContext context,
        CancellationToken cancellationToken = default);
}

public class ProcessResult
{
    public string? CorrelationId { get; init; }
    public ProcessStepOutcome Outcome { get; init; }
    public IReadOnlyCollection<ProcessStepMessage> Messages { get; init; } = [];
    public IReadOnlyCollection<ProcessStepDefinition> RequiredSteps { get; init; } = [];
}

public class ParticipantDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyCollection<ProcessStepDefinition> Steps { get; init; } = [];
}

public class ProcessStepDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyCollection<FieldDefinition> Fields { get; set; } = [];
}

public class FieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public Type Type { get; set; } = typeof(string);
    public bool IsRequired { get; set; } = false;
}

public sealed record ParticipantRequest
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Steps { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.OrdinalIgnoreCase);
}

