namespace Kaleido.Samples.PriorAuth.Configuration.Data.Entities;

public sealed class QuestionnaireDefinitionItemAnswerOption
{
    public Guid QuestionnaireDefinitionItemAnswerOptionId { get; set; }

    public Guid QuestionnaireDefinitionItemId { get; set; }

    public string Value { get; set; } = string.Empty;

    public string DisplayText { get; set; } = string.Empty;

    public int Order { get; set; }

    public QuestionnaireDefinitionItem QuestionnaireDefinitionItem { get; set; } = null!;
}
