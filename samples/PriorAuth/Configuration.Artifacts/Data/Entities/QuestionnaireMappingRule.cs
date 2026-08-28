namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;

public sealed class QuestionnaireMappingRule
{
    public Guid QuestionnaireMappingRuleId { get; set; }

    public Guid QuestionnaireDefinitionId { get; set; }

    public string StepName { get; set; } = string.Empty;

    public string? PlanId { get; set; }

    public string? LineOfBusiness { get; set; }

    public ProcedureModality? ProcedureModality { get; set; }

    public string? ProcedureCodeValue { get; set; }

    public int Priority { get; set; }

    public bool IsActive { get; set; }

    public QuestionnaireDefinition QuestionnaireDefinition { get; set; } = null!;
}
