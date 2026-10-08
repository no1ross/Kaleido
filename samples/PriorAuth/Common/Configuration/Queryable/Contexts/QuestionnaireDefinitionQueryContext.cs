using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

// Service-to-service only: Radiology resolves questionnaires into step results.
public sealed class QuestionnaireDefinitionQueryContext : IQueryContext
{
    [Key]
    public Guid QuestionnaireDefinitionId { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals)]
    public string QuestionnaireId { get; init; } = string.Empty;

    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals)]
    public string Version { get; init; } = string.Empty;

    [Searchable(Priority = 3, MatchMode = MatchMode.Contains)]
    [Filterable(FilterOperator.Equals)]
    public string Name { get; init; } = string.Empty;

    [Searchable(Priority = 4, MatchMode = MatchMode.Contains)]
    public string Title { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    public bool IsActive { get; init; }
}
