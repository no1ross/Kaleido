using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Models;

public sealed class RequestingProviderSearchQueryParameters
{
    [Required]
    public Guid ProcessId { get; init; }
}
