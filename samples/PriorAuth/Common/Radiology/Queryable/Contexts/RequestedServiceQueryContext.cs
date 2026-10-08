using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Radiology.Queryable.Contexts;

public sealed class RequestedServiceQueryContext : IQueryContext
{
    [Key]
    public Guid PriorAuthorizationRequestedServiceId { get; init; }

    [Filterable(FilterOperator.Equals)]
    public Guid PriorAuthorizationId { get; init; }

    [Filterable(FilterOperator.Equals)]
    public Guid ProcessId { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    public string UserEnteredCodeValue { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    public ProcedureCodeSystem UserEnteredCodeSystem { get; init; }

    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    public string ResolvedCodeValue { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    public ProcedureCodeSystem ResolvedCodeSystem { get; init; }

    [Searchable(Priority = 3, MatchMode = MatchMode.Contains)]
    public string Description { get; init; } = string.Empty;
}
