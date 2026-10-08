using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed class RequestingProviderSearchQueryParameters : IQueryParameters
{
    [Required]
    public Guid ProcessId { get; init; }
}
