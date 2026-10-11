using Kaleido.Queryable;
namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;

public sealed record QuestionnaireDefinitionViewParameters : IQueryParameters
{
    /// <summary>A specific questionnaire (e.g. the next round of a clinical pathway); the rules are not used.</summary>
    public string? QuestionnaireId { get; init; }

    public string StepName { get; init; } = string.Empty;

    public string? PlanId { get; init; }

    public string? LineOfBusiness { get; init; }

    public ProcedureModality? ProcedureModality { get; init; }

    public string? ProcedureCodeValue { get; init; }
}
