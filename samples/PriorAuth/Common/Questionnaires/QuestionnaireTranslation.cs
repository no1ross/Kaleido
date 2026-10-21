using Kaleido.Samples.PriorAuth.Configuration.Queryable.ViewSources.Views;

namespace Kaleido.Samples.PriorAuth.Questionnaires;

/// <summary>
/// Translates a configured questionnaire (the Configuration service's FHIR-like format) into
/// Kaleido's information request, generically: no question, item id or wording in code.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><description>An item's id is its configured <c>BindingKey</c> (its <c>LinkId</c> when it
/// has none), so handlers read answers by the business's keys.</description></item>
/// <item><description>A question whose binding key matches a detail the caller already stated
/// proposes that value when it fits; the caller confirms it. Configured defaults are never
/// proposed.</description></item>
/// <item><description>Text may contain tokens filled from the request (e.g. <c>{ProcedureCode}</c>).</description></item>
/// <item><description>Conditions (enableWhen) are not carried yet (#235).</description></item>
/// </list>
/// </remarks>
public static class QuestionnaireTranslation
{
    public static InformationRequest ToInformationRequest(
        QuestionnaireDefinitionView questionnaire,
        IReadOnlyDictionary<string, string>? stated = null,
        IReadOnlyDictionary<string, string>? tokens = null)
    {
        stated ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        tokens ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new InformationRequest
        {
            InformationRequestId = $"{questionnaire.QuestionnaireId}|{questionnaire.Version}",
            Title = Fill(questionnaire.Title, tokens),
            Items = questionnaire.Items
                .OrderBy(x => x.Order)
                .Select(item => ToItem(item, stated, tokens))
                .ToArray()
        };
    }

    /// <summary>The answers to the question with <paramref name="bindingKey"/>.</summary>
    public static IReadOnlyList<string> Answers(
        IInformationStep answers,
        string bindingKey) =>
        answers.Items
            .FirstOrDefault(x => string.Equals(x.ItemId, bindingKey, StringComparison.OrdinalIgnoreCase))?
            .Answers
            .Select(x => x.Value)
            .ToArray()
        ?? [];

    /// <summary>The first answer to the question with <paramref name="bindingKey"/>.</summary>
    public static string? Answer(
        IInformationStep answers,
        string bindingKey) =>
        Answers(answers, bindingKey).FirstOrDefault();

    private static InformationItem ToItem(
        QuestionnaireItemView item,
        IReadOnlyDictionary<string, string> stated,
        IReadOnlyDictionary<string, string> tokens)
    {
        var type = ToItemType(item.Type);

        var options = item.AnswerOptions
            .OrderBy(x => x.Order)
            .Select(option => new InformationOption { Value = option.Value, Display = option.DisplayText })
            .ToArray();

        var proposal =
            !string.IsNullOrWhiteSpace(item.BindingKey) &&
            stated.TryGetValue(item.BindingKey, out var value) &&
            Fits(type, options, value)
                ? value
                : null;

        return new InformationItem
        {
            Id = string.IsNullOrWhiteSpace(item.BindingKey) ? item.LinkId : item.BindingKey,
            Text = Fill(item.Text, tokens),
            Type = type,
            Repeats = item.Repeats,
            Options = options,
            ProposedAnswers = proposal is null ? [] : [new InformationAnswer { Value = proposal }],
            // The configured conditions, on the binding keys the item ids are made of; Kaleido decides
            // from them which questions are asked (ADR 0024).
            EnableWhen = item.EnableWhen
                .Select(condition => new InformationCondition
                {
                    Question = condition.QuestionBindingKey,
                    Operator = ToOperator(condition.Operator),
                    Answer = condition.AnswerValue
                })
                .ToArray()
        };
    }

    // The operator names the configuration uses (enum names or FHIR's symbols). A name it does not
    // know is left undefined, so Kaleido rejects the request as not well-formed instead of the
    // question silently being asked always.
    private static InformationConditionOperator ToOperator(
        string value) =>
        value.Trim() switch
        {
            "=" => InformationConditionOperator.Equals,
            "!=" => InformationConditionOperator.NotEquals,
            ">" => InformationConditionOperator.GreaterThan,
            "<" => InformationConditionOperator.LessThan,
            ">=" => InformationConditionOperator.GreaterThanOrEqual,
            "<=" => InformationConditionOperator.LessThanOrEqual,
            var name when Enum.TryParse<InformationConditionOperator>(name, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) => parsed,
            _ => (InformationConditionOperator)(-1)
        };

    // Only a value the caller could submit unchanged is proposed.
    private static bool Fits(
        InformationItemType type,
        IReadOnlyList<InformationOption> options,
        string value) =>
        type switch
        {
            InformationItemType.Choice => options.Any(x => string.Equals(x.Value, value, StringComparison.Ordinal)),
            InformationItemType.Boolean => bool.TryParse(value, out _),
            InformationItemType.Display or InformationItemType.Group => false,
            _ => !string.IsNullOrWhiteSpace(value)
        };

    private static string Fill(
        string? text,
        IReadOnlyDictionary<string, string> tokens) =>
        tokens.Aggregate(
            text ?? string.Empty,
            (current, token) => current.Replace($"{{{token.Key}}}", token.Value, StringComparison.OrdinalIgnoreCase));

    private static InformationItemType ToItemType(
        string type) =>
        type.ToLowerInvariant() switch
        {
            "choice" => InformationItemType.Choice,
            "boolean" => InformationItemType.Boolean,
            "integer" => InformationItemType.WholeNumber,
            "decimal" => InformationItemType.Number,
            "date" => InformationItemType.Date,
            "datetime" => InformationItemType.DateTime,
            "text" => InformationItemType.LongText,
            "display" => InformationItemType.Display,
            "group" => InformationItemType.Group,
            _ => InformationItemType.Text
        };
}
