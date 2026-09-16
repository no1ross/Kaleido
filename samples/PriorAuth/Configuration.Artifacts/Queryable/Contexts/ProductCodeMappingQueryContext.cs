using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Metadata;
using Kaleido.Samples.PriorAuth;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

[QueryContext(
    Name = "product-code-mappings",
    DisplayName = "Product Code Mappings",
    Version = "1.0.0",
    Source = "Prior Authorization Configuration",
    Kind = QueryContextKind.Direct)]
public sealed class ProductCodeMappingQueryContext
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
