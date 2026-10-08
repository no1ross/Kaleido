using Kaleido.Queryable;
namespace Kaleido.Samples.PriorAuth.Provider.Queryable.Parameters;

public sealed class RequestingProviderSearchParameters : IQueryParameters
{
    public string PlanId { get; init; } = string.Empty;
}
