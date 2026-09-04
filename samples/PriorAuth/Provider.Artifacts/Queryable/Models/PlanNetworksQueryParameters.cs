using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Provider.Queryable.Models;

public sealed class PlanNetworksQueryParameters
{
    [Required]
    public string PlanId { get; init; } = string.Empty;
}
