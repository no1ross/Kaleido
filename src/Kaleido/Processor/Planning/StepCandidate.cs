using Kaleido.Processor.Registry;

namespace Kaleido.Processor.Planning;

internal sealed class StepCandidate
{
    private readonly List<StepProcessingMessage> _messages = [];

    public required string StepName { get; init; }

    public ProcessStepRegistration? Registration { get; init; }

    public bool IncludedInExecutionPlan { get; set; }

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
        return Step is TStep step
            ? step
            : throw new KaleidoFrameworkException(
                FrameworkErrorCodes.TypeMismatch,
                $"Step candidate '{StepName}' has no hydrated step of type '{typeof(TStep).Name}'.");
    }

    public void AddMessage(
        StepProcessingMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _messages.Add(message);
    }

    public void AddInformation(
        string code,
        string message)
    {
        AddMessage(
            StepProcessingMessage.Information(
                code,
                message));
    }

    public void AddWarning(
        string code,
        string message)
    {
        AddMessage(
            StepProcessingMessage.Warning(
                code,
                message));
    }

    public void AddError(
        string code,
        string message)
    {
        AddMessage(
            StepProcessingMessage.Error(
                code,
                message));
    }

    public static StepCandidate Invalid(
        string stepName,
        string code,
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
        string code,
        string message)
    {
        Status = StepCandidateStatus.Invalid;

        AddError(
            code,
            message);
    }
}
