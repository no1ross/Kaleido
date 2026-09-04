using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Samples.PriorAuth.CodeSet;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Radiology.Queryable.Contexts;

[QueryContext(
    Name = "requested-services",
    DisplayName = "Intake - Requested Services",
    Version = "1.0.0",
    Source = "Prior Authorization Intake",
    Kind = QueryContextKind.Direct)]
public sealed class RequestedServiceQueryContext
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
