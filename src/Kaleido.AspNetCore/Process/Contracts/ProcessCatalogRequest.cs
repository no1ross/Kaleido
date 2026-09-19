namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessCatalogRequest
{
    public IReadOnlyCollection<ProcessStepSummary> InitialSteps
    {
        get;
        init;
    }
        = [];
}