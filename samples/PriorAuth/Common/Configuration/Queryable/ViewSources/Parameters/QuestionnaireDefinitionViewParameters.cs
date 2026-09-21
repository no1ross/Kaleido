namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Parameters;

public sealed record QuestionnaireDefinitionViewParameters
{
    public string StepName { get; init; } = string.Empty;

    public string? PlanId { get; init; }

    public string? LineOfBusiness { get; init; }

    public ProcedureModality? ProcedureModality { get; init; }

    public string? ProcedureCodeValue { get; init; }
}
