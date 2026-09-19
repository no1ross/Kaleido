using Kaleido.Process.Registry;

namespace Kaleido.Process.Planning;

public sealed class StepCandidate
{
    private readonly List<StepProcessingMessage> _messages = [];

    public required string StepName { get; init; }

    public ProcessStepRegistration? Registration { get; init; }

    public bool IncludedInExecutionPlan { get; set; } = false;

    public object? Step { get; set; }

    public StepCandidateStatus Status { get; set; } =
        StepCandidateStatus.Pending;

    public IReadOnlyCollection<StepProcessingMessage> Messages =>
        _messages;

    public bool HasErrors =>
        _messages.Any(x => x.Type == MessageType.Error);

    public TStep GetStep<TStep>()
        where TStep : class
    {
        return (TStep)Step!;
    }

    public void AddMessage(
        StepProcessingMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _messages.Add(message);
    }

    public void AddInformation(
        StepProcessingMessageCode code,
        string message)
    {
        AddMessage(
            StepProcessingMessage.Information(
                code,
                message));
    }

    public void AddWarning(
        StepProcessingMessageCode code,
        string message)
    {
        AddMessage(
            StepProcessingMessage.Warning(
                code,
                message));
    }

    public void AddError(
        StepProcessingMessageCode code,
        string message)
    {
        AddMessage(
            StepProcessingMessage.Error(
                code,
                message));
    }

    public static StepCandidate Invalid(
        string stepName,
        StepProcessingMessageCode code,
        string message)
    {
        var candidate =
            new StepCandidate
            {
                StepName = stepName
            };

        candidate.MarkInvalid(
            code,
            message);

        return candidate;
    }

    public void MarkInvalid(
        StepProcessingMessageCode code,
        string message)
    {
        Status = StepCandidateStatus.Invalid;

        AddError(
            code,
            message);
    }
}
