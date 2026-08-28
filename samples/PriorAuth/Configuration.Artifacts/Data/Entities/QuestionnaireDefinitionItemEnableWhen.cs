namespace Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;

public sealed class QuestionnaireDefinitionItemEnableWhen
{
    public Guid QuestionnaireDefinitionItemEnableWhenId { get; set; }

    public Guid QuestionnaireDefinitionItemId { get; set; }

    public string QuestionBindingKey { get; set; } = string.Empty;

    public string Operator { get; set; } = string.Empty;

    public string AnswerValue { get; set; } = string.Empty;

    public QuestionnaireDefinitionItem QuestionnaireDefinitionItem { get; set; } = null!;
}
