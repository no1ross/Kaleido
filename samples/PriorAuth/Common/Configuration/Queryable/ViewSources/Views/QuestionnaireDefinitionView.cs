using System.ComponentModel;

namespace Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;

public sealed record QuestionnaireDefinitionView
{
    [Description("The identifier of the questionnaire, shared by all its versions.")]
    public string QuestionnaireId { get; init; } = string.Empty;

    [Description("The version of the questionnaire definition.")]
    public string Version { get; init; } = string.Empty;

    [Description("The internal name of the questionnaire.")]
    public string Name { get; init; } = string.Empty;

    [Description("The title shown to the person answering the questionnaire.")]
    public string Title { get; init; } = string.Empty;

    [Description("The description of the questionnaire; null if none is defined.")]
    public string? Description { get; init; }

    [Description("The questions of the questionnaire, in display order, each with its answer options and enable-when conditions.")]
    public QuestionnaireItemView[] Items { get; init; } = [];
}

public sealed record QuestionnaireItemView
{
    public string LinkId { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string BindingKey { get; init; } = string.Empty;

    public bool Required { get; init; }

    public bool Repeats { get; init; }

    public string? DefaultValue { get; init; }

    public int Order { get; init; }

    public QuestionnaireAnswerOptionView[] AnswerOptions { get; init; } = [];

    public QuestionnaireEnableWhenView[] EnableWhen { get; init; } = [];
}

public sealed record QuestionnaireEnableWhenView
{
    public string QuestionBindingKey { get; init; } = string.Empty;

    public string Operator { get; init; } = string.Empty;

    public string AnswerValue { get; init; } = string.Empty;
}

public sealed record QuestionnaireAnswerOptionView
{
    public string Value { get; init; } = string.Empty;

    public string DisplayText { get; init; } = string.Empty;

    public int Order { get; init; }
}
