namespace Kaleido.Queryable;

/// <summary>
/// Marks a record as a <b>query context</b>: the record that describes how a query source can be
/// queried. Its properties carry the query rules — <see cref="FilterableAttribute"/>,
/// <see cref="SearchableAttribute"/>, <see cref="SortableAttribute"/> — and it is the record type a
/// local source produces and a direct query returns.
/// </summary>
/// <remarks>
/// <para>
/// A query context is not a capability on its own and has no public name; a source
/// (<see cref="IQuerySource{TQueryContext}"/>, <see cref="IQuerySourceAsync{TQueryContext}"/> or
/// <see cref="IDelegatedQuerySource{TQueryContext,TResult,TParameters}"/>) is. The same context may
/// be used by several sources.
/// </para>
/// <para>
/// The query-rule attributes only have meaning on query contexts; an analyzer reports them on
/// other types.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed record MemberQueryContext : IQueryContext
/// {
///     [Key] public Guid MemberEnrollmentId { get; init; }
///
///     [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
///     [Sortable]
///     public string MemberNumber { get; init; } = string.Empty;
/// }
/// </code>
/// </example>
public interface IQueryContext;
