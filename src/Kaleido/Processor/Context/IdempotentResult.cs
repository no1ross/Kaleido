namespace Kaleido.Processor.Context;

/// <summary>
/// The stored answer to a request sent with an idempotency key: a retry with the same key gets this
/// answer again instead of running the step a second time.
/// </summary>
/// <remarks>
/// Kept by the <see cref="IKaleidoContextStore"/> next to the process state (the SQLite reference
/// store: its own table in the same database) until <see cref="ExpiresUtc"/>. The transport decides
/// what the key's scope is and what the answer looks like; the store only keeps it.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record IdempotentResult
{
    /// <summary>The scoped idempotency key (unique per service and caller).</summary>
    public required string Key { get; init; }

    /// <summary>A hash of the request the key was first used with; the same key with another request is refused.</summary>
    public required string RequestHash { get; init; }

    /// <summary>The status of the stored answer (on HTTP, the response status code).</summary>
    public required int StatusCode { get; init; }

    /// <summary>The stored answer as it was returned (on HTTP, the JSON response body).</summary>
    public required string Response { get; init; }

    /// <summary>When the stored answer is no longer returned and may be deleted.</summary>
    public required DateTimeOffset ExpiresUtc { get; init; }
}