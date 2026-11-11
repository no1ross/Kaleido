namespace Kaleido.Queryable;

/// <summary>
/// Marks a record as the <b>parameters</b> of a query view or delegated source: named inputs
/// beyond search, filter, sort and paging (for example a process id). Parameters are published in
/// the registry with their validation constraints.
/// </summary>
/// <remarks>
/// Use <see cref="EmptyQueryViewParameters"/> (or the generic overloads without a parameters type
/// argument) when no parameters are needed.
/// </remarks>
public interface IQueryParameters;

/// <summary>Parameters record used when a query view or delegated source declares no parameters.</summary>
[ExcludeFromCodeCoverage]
public sealed record EmptyQueryViewParameters : IQueryParameters;
