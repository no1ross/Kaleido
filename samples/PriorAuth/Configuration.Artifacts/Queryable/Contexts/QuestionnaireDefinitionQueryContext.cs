using Kaleido.Queryable;
using Kaleido.Queryable.Attributes;
using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Queryable.Contexts;

[QueryContext(
    Name = "questionnaire-definitions",
    DisplayName = "Questionnaire Definitions",
    Version = "1.0.0",
    Source = "Prior Authorization Configuration",
    AllowDirectQuery = true)]
public sealed class QuestionnaireDefinitionQueryContext
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
