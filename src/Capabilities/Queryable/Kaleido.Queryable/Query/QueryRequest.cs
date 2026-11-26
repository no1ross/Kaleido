namespace Kaleido.Queryable.Query;

/// <summary>
/// A query request whose parameters record is statically typed as <typeparamref name="TParameters"/>.
/// </summary>
/// <typeparam name="TParameters">
/// The parameters record of the query view or delegated source being queried
/// (<see cref="EmptyQueryViewParameters"/> when it declares none).
/// </typeparam>
/// <remarks>
/// A delegated source receives its request in this form; the request's parameters type must be the
/// source's declared parameters type.
/// </remarks>
public interface IQueryRequest<out TParameters> : IQueryRequest
    where TParameters : class
{
    /// <summary>
    /// The typed parameters record, or <see langword="null"/> when the caller supplied none. A
    /// missing record is rejected during validation only when the record has a required member.
    /// </summary>
    new TParameters? ViewParameters { get; }
}

/// <summary>
/// A query against a Queryable source or view: the query body (search, filter, sort, paging) and
/// the parameters record. This is the untyped view of the request that the runtime validates,
/// compiles and passes to sources and views through <see cref="QueryExecutionContext"/>.
/// </summary>
/// <remarks>
/// Core request types are not serialized over HTTP directly; the transport converts its own wire
/// contract into a request before invoking the runtime.
/// </remarks>
public interface IQueryRequest
{
    /// <summary>
    /// The search, filter, sort and paging to apply, or <see langword="null"/> for none (every
    /// result, in the capability's default order and default page when it is pageable).
    /// </summary>
    QueryBody? Query { get; }

    /// <summary>
    /// The parameters record as an <see cref="object"/>, or <see langword="null"/> when none was
    /// supplied. Its DataAnnotations are validated before the query runs.
    /// </summary>
    object? ViewParameters { get; }

    /// <summary>
    /// The declared parameters type of the request (the <c>TParameters</c> of
    /// <see cref="IQueryRequest{TParameters}"/>), available even when
    /// <see cref="ViewParameters"/> is <see langword="null"/>. Parameter validation is skipped when
    /// it is <see cref="EmptyQueryViewParameters"/>.
    /// </summary>
    Type ViewParametersType { get; }
}

/// <summary>
/// A query request for a source or view that declares no parameters; its parameters record is
/// always an <see cref="EmptyQueryViewParameters"/> instance.
/// </summary>
/// <param name="Query">The search, filter, sort and paging to apply, or <see langword="null"/> for none.</param>
[ExcludeFromCodeCoverage]
public sealed record QueryRequest(
    QueryBody? Query = null)
    : QueryRequest<EmptyQueryViewParameters>(
        ViewParameters: new EmptyQueryViewParameters(),
        Query: Query);
