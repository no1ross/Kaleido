using Kaleido.Http.Abstractions.Process.Contracts;
using Kaleido.Http.Abstractions.Queryable.Contracts;

namespace Kaleido.Http.Abstractions.Registry;

/// <summary>
/// The response shape for the unified registry endpoint.
/// Contains process registrations (local + downstream), queryable registrations
/// (local + downstream), and any client errors that occurred during aggregation.
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
    /// All queryable context registrations from this processor's local contexts
    /// and all downstream queryable clients registered via <c>AddQueryableClient()</c>.
    /// </summary>
    public IReadOnlyCollection<QueryableRecordResponse> Queryables { get; init; }
        = [];

    /// <summary>
    /// Errors that occurred while aggregating registrations from downstream clients.
    /// A non-empty collection means the response is partial — one or more downstream
    /// clients were unreachable or returned an error. The <see cref="Processes"/> and
    /// <see cref="Queryables"/> collections contain only the data that was successfully
    /// retrieved; entries for the failing clients are absent.
    /// </summary>
    public IReadOnlyCollection<RegistryClientError> ClientErrors { get; init; }
        = [];
}

/// <summary>
/// Describes a single downstream client that failed during registry aggregation.
/// </summary>
public sealed record RegistryClientError
{
    /// <summary>
    /// The registered name of the downstream client (e.g. <c>"Member"</c>, <c>"CodeSet"</c>).
    /// Matches the <c>Name</c> passed to <c>AddProcessClient()</c> or <c>AddQueryableClient()</c>.
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// The category of client that failed.
    /// Either <c>"Process"</c> or <c>"Queryable"</c>.
    /// </summary>
    public required string ClientType { get; init; }

    /// <summary>
    /// A human-readable description of the failure.
    /// </summary>
    public required string Reason { get; init; }
}
