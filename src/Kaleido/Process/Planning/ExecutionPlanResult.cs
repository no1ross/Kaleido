namespace Kaleido.Process.Planning;

internal sealed record ExecutionPlanResult
{
    public required IReadOnlyCollection<StepCandidate> Candidates
    {
        get;
        init;
    }
     = [];
}