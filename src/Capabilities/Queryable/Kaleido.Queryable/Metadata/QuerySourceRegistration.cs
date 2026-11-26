using Kaleido.Registry;

namespace Kaleido.Queryable.Metadata;

/// <summary>
/// How a query source produces its results. Derived from the interface the source implements;
/// it is never declared by the developer.
/// </summary>
/// <remarks>
/// This is runtime metadata for the service and its developers (available to sources and views
/// through <c>QueryExecutionContext.Metadata</c>). It is not published to consumers: the registry
/// and transports present every source the same way.
/// </remarks>
internal enum QuerySourceKind
{
    /// <summary>
    /// A local source (<see cref="IQuerySource{TQueryContext}"/> or
    /// <see cref="IQuerySourceAsync{TQueryContext}"/>): Kaleido applies search, filter, sort and
    /// paging in-process. It can be queried directly and through its views.
    /// </summary>
    Local,

    /// <summary>
    /// A delegated source (<see cref="IDelegatedQuerySource{TQueryContext,TResult,TParameters}"/>):
    /// the source executes the query downstream and returns its own mapped results. It has no views.
    /// </summary>
    Delegated,
}

/// <summary>A discovered query source and its metadata.</summary>
/// <param name="SourceType">The source type (the capability's identity).</param>
/// <param name="QueryContextType">The source's query context record.</param>
/// <param name="ResultType">
/// The record type a direct query returns: the query context for a local source, or the source's own
/// result record for a delegated source.
/// </param>
/// <param name="ParametersType">The source's parameters record (<see cref="EmptyQueryViewParameters"/> for local sources).</param>
/// <param name="Metadata">The source's published metadata.</param>
[ExcludeFromCodeCoverage]
internal sealed record QuerySourceRegistration(
    Type SourceType,
    Type QueryContextType,
    Type ResultType,
    Type ParametersType,
    QuerySourceMetadata Metadata);

/// <summary>Published metadata for a query source.</summary>
/// <param name="Name">The public name (the source's type name).</param>
/// <param name="Description">What the source provides.</param>
/// <param name="DisplayName">The human-readable name.</param>
/// <param name="Source">Optional description of where the data comes from.</param>
/// <param name="Kind">Local or delegated (derived).</param>
/// <param name="Pageable">Paging for direct queries of this source; <see langword="null"/> when not pageable.</param>
/// <param name="Fields">The query context fields and their query rules.</param>
/// <param name="Parameters">The source's parameters (delegated sources only; empty otherwise).</param>
/// <param name="OutputFields">The fields of the record a direct query returns.</param>
/// <param name="Authorization">The effective authorization rule.</param>
[ExcludeFromCodeCoverage]
internal sealed record QuerySourceMetadata(
    string Name,
    string Description,
    string DisplayName,
    string? Source,
    QuerySourceKind Kind,
    PageableMetadata? Pageable,
    IReadOnlyList<FieldMetadata> Fields,
    IReadOnlyList<QueryParameterMetadata> Parameters,
    IReadOnlyList<QueryOutputFieldMetadata> OutputFields,
    AuthorizationMetadata Authorization);
