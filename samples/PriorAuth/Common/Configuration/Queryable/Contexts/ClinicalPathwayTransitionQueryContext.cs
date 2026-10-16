using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.Contexts;

// Service-to-service only: read by the clinical pathway provider to decide the next questions.
public sealed class ClinicalPathwayTransitionQueryContext : IQueryContext
{
    [Key]
    [Description("The unique identifier of the clinical pathway transition.")]
    public Guid ClinicalPathwayTransitionId { get; init; }

    [Description("The questionnaire whose answers this transition evaluates.")]
    [Filterable(FilterOperator.Equals)]
    public string FromQuestionnaireId { get; init; } = string.Empty;

    [Description("The binding key of the answer the condition tests; null for an unconditional transition.")]
    public string? WhenBindingKey { get; init; }

    [Description("Equals or NotEquals.")]
    public string Operator { get; init; } = string.Empty;

    [Description("The answer value the condition compares with.")]
    public string? AnswerValue { get; init; }

    [Description("The next questionnaire; null when the pathway finishes here.")]
    public string? ToQuestionnaireId { get; init; }

    [Description("The outcome recorded when the pathway finishes on this transition.")]
    public string? Determination { get; init; }

    [Description("Evaluation order: higher first.")]
    [Sortable]
    public int Priority { get; init; }
}
