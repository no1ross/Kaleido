namespace Kaleido.Samples.PriorAuth.Configuration.Data.Entities;

public sealed class QuestionnaireDefinition
{
    public Guid QuestionnaireDefinitionId { get; set; }

    public string QuestionnaireId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public ICollection<QuestionnaireDefinitionItem> Items { get; set; } = new List<QuestionnaireDefinitionItem>();

    public ICollection<QuestionnaireMappingRule> MappingRules { get; set; } = new List<QuestionnaireMappingRule>();
}
