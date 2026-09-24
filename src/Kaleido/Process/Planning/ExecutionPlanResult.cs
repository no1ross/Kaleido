namespace Kaleido.Process.Planning;

[ExcludeFromCodeCoverage]
internal sealed record ExecutionPlanResult
{
    public required IReadOnlyCollection<StepCandidate> Candidates
    {
        get;
        init;
    }
     = [];
}