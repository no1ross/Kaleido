using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.PriorAuth.Provider.Queryable.Parameters;

public sealed class RequestingProviderSearchParameters : IQueryParameters
{
    [Required]
    [Description("The member's plan; network status is derived from its networks.")]
    public string PlanId { get; init; } = string.Empty;

    [Description("Which providers to search: requesting providers (the default) or servicing facilities.")]
    public ProviderType ProviderType { get; init; } = ProviderType.RequestingProvider;
}
