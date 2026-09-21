namespace Kaleido.Samples.PriorAuth.Configuration.Data.Entities;

public sealed class QuestionnaireDefinitionItem
{
    public Guid QuestionnaireDefinitionItemId { get; set; }

    public Guid QuestionnaireDefinitionId { get; set; }

    public string LinkId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string BindingKey { get; set; } = string.Empty;

    public bool Required { get; set; }

    public bool Repeats { get; set; }

    public string? DefaultValue { get; set; }

    public int Order { get; set; }

    public QuestionnaireDefinition QuestionnaireDefinition { get; set; } = null!;

    public ICollection<QuestionnaireDefinitionItemAnswerOption> AnswerOptions { get; set; } = new List<QuestionnaireDefinitionItemAnswerOption>();

    public ICollection<QuestionnaireDefinitionItemEnableWhen> EnableWhen { get; set; } = new List<QuestionnaireDefinitionItemEnableWhen>();
}
