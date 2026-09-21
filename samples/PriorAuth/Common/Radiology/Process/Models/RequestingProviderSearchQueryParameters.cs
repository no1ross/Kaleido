using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed class RequestingProviderSearchQueryParameters
{
    [Required]
    public Guid ProcessId { get; init; }
}
