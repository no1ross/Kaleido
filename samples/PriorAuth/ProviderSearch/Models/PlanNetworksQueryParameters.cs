using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Models;

public sealed class PlanNetworksQueryParameters
{
    [Required]
    public string PlanId { get; init; } = string.Empty;
}
