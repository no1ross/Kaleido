namespace Kaleido.Process.Participant.Planning;

internal sealed record ExecutionPlanResult
{
    public required IReadOnlyCollection<StepCandidate> Candidates
    {
        get;
        init;
    }
     = [];
}