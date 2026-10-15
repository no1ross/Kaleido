using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed class RequestingProviderSearchQueryParameters : IQueryParameters
{
    // Nullable so [Required] can fail: on a plain Guid an omitted value is Guid.Empty and counts as present.
    [Required]
    [Description("The processId of the prior authorization the search is for; results are scoped to its member's plan.")]
    public Guid? ProcessId { get; init; }
}
