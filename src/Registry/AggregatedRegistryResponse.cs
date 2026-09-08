using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Queryable.AspNetCore.Contracts;

namespace Kaleido.Registry;

/// <summary>
/// The response shape for the unified registry endpoint.
/// Contains both process and queryable registrations aggregated
/// from all registered downstream clients.
/// </summary>
public sealed record AggregatedRegistryResponse
{
    /// <summary>
    /// All process processor registrations, including this processor's local steps
    /// and all downstream processors registered via <c>AddProcessClient()</c>.
    /// </summary>
    public IReadOnlyCollection<ProcessorRegistryResponse> Processes { get; init; }
        = [];

    /// <summary>
    /// All queryable context registrations from all queryable clients
    /// registered via <c>AddQueryableClient()</c>.
    /// </summary>
    public IReadOnlyCollection<QueryableRecordResponse> Queryables { get; init; }
        = [];
}
