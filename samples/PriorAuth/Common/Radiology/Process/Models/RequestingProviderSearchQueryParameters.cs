using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed class RequestingProviderSearchQueryParameters : IQueryParameters
{
    [Required]
    [Description("The processId of the prior authorization the search is for; results are scoped to its member's plan.")]
    public Guid ProcessId { get; init; }
}
