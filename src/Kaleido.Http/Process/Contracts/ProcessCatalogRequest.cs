using Kaleido.Http.Abstractions.Process.Contracts;

namespace Kaleido.Http.Process.Contracts;

public sealed record ProcessCatalogRequest
{
    public IReadOnlyCollection<ProcessStepSummary> InitialSteps
    {
        get;
        init;
    }
        = [];
}
