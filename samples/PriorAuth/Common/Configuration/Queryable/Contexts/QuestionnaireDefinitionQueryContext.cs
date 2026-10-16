using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

// Service-to-service only: Radiology resolves questionnaires into step results.
public sealed class QuestionnaireDefinitionQueryContext : IQueryContext
{
    [Key]
    [Description("The unique identifier of the questionnaire definition record.")]
    public Guid QuestionnaireDefinitionId { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals)]
    [Description("The identifier of the questionnaire, shared by all its versions.")]
    public string QuestionnaireId { get; init; } = string.Empty;

    [Searchable(Priority = 2, MatchMode = MatchMode.Exact)]
    [Filterable(FilterOperator.Equals)]
    [Description("The version of the questionnaire definition.")]
    public string Version { get; init; } = string.Empty;

    [Searchable(Priority = 3, MatchMode = MatchMode.Contains)]
    [Filterable(FilterOperator.Equals)]
    [Description("The internal name of the questionnaire.")]
    public string Name { get; init; } = string.Empty;

    [Searchable(Priority = 4, MatchMode = MatchMode.Contains)]
    [Description("The title shown to the person answering the questionnaire.")]
    public string Title { get; init; } = string.Empty;

    [Filterable(FilterOperator.Equals)]
    [Description("Whether this questionnaire definition is currently in use.")]
    public bool IsActive { get; init; }
}
