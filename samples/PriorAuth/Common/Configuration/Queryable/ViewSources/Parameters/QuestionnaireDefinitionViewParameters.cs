using System.ComponentModel;
using Kaleido.Queryable;
namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;

public sealed record QuestionnaireDefinitionViewParameters : IQueryParameters
{
    /// <summary>A specific questionnaire (e.g. the next round of a clinical pathway); the rules are not used.</summary>
    [Description("A specific questionnaire to return; when set, the step and the matching rules are ignored.")]
    public string? QuestionnaireId { get; init; }

    [Description("The process step that needs a questionnaire; used to pick the matching rule.")]
    public string StepName { get; init; } = string.Empty;

    [Description("The member's plan, used to pick a plan-specific questionnaire.")]
    public string? PlanId { get; init; }

    [Description("The member's line of business, used to pick a questionnaire for that line of business.")]
    public string? LineOfBusiness { get; init; }

    [Description("The modality of the requested procedure, such as MRI or CT, used to pick the questionnaire.")]
    public ProcedureModality? ProcedureModality { get; init; }

    [Description("The requested procedure code, used to pick a code-specific questionnaire.")]
    public string? ProcedureCodeValue { get; init; }
}
