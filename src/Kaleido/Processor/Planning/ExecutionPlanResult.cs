namespace Kaleido.Processor.Planning;

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