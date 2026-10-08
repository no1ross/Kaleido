using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

// Service-to-service only: queried by Intake to route to a domain processor.
public sealed class ProductCodeMappingQueryContext : IQueryContext
{
    [Key]
    public Guid ProductCodeMappingId { get; init; }

    [Filterable(FilterOperator.Equals)]
    public ProcedureCodeSystem CodeSystem { get; init; }

    [Filterable(FilterOperator.Equals, FilterOperator.In)]
    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    public string CodeValue { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    public string ProcessorName { get; init; } = string.Empty;
}
