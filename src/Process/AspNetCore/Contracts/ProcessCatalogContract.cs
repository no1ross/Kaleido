namespace Kaleido.Process.AspNetCore.Contracts;

public sealed record ProcessCatalogContract
{
    public IReadOnlyCollection<ProcessStepSummaryContract> InitialSteps
    {
        get;
        init;
    }
        = [];
}