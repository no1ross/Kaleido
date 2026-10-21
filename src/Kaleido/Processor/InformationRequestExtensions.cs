using System.Globalization;

namespace Kaleido.Processor;

// Which questions of an information request apply (ADR 0024): one evaluation for answer validation
// and for the MCP adapter's rounds, so Kaleido - never an agent - decides what is asked. Items are
// walked in presentation order; a condition refers only to an earlier question, so one pass decides
// everything, and the answer of a disabled question counts as no answer.
internal static class InformationRequestExtensions
{
    // The questions (not groups or display text), in presentation order.
    public static IReadOnlyList<InformationItem> Questions(this InformationRequest request) =>
        [.. Flatten(request.Items, []).Select(x => x.Item).Where(IsQuestion)];

    // The ids of the items that apply given the answers (questions, groups and display text).
    public static IReadOnlySet<string> EnabledItemIds(
        this InformationRequest request,
        IReadOnlyDictionary<string, IReadOnlyList<string>> answers)
    {
        var questions = Flatten(request.Items, [])
            .Select(x => x.Item)
            .Where(IsQuestion)
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

        var enabled = new HashSet<string>(StringComparer.Ordinal);
        Evaluate(request.Items, parentEnabled: true, questions, answers, enabled);
        return enabled;
    }

    // The questions to ask next: enabled, unanswered, and decided - every question their conditions
    // (and their groups' conditions) test is answered or disabled. The first round is the
    // unconditional questions; each answer can decide further questions.
    public static IReadOnlyList<InformationItem> AskableQuestions(
        this InformationRequest request,
        IReadOnlyDictionary<string, IReadOnlyList<string>> answers)
    {
        var enabled = request.EnabledItemIds(answers);

        return
        [
            .. Flatten(request.Items, [])
                .Where(x =>
                    IsQuestion(x.Item) &&
                    enabled.Contains(x.Item.Id) &&
                    !IsAnswered(answers, x.Item.Id) &&
                    x.Conditions.All(condition => !enabled.Contains(condition.Question) || IsAnswered(answers, condition.Question)))
                .Select(x => x.Item),
        ];
    }

    // Whether a condition holds for the answers of its tested question (none when unanswered or disabled).
    public static bool Holds(this InformationCondition condition, InformationItem question, IReadOnlyList<string> answers)
    {
        if (condition.Operator == InformationConditionOperator.Exists)
        {
            return answers.Count > 0 == string.Equals(condition.Answer, "true", StringComparison.OrdinalIgnoreCase);
        }

        if (answers.Count == 0 || condition.Answer is not { } expected)
        {
            return false;
        }

        return condition.Operator switch
        {
            InformationConditionOperator.Equals => answers.Any(x => Compare(question.Type, x, expected) == 0),
            InformationConditionOperator.NotEquals => answers.All(x => Compare(question.Type, x, expected) != 0),
            InformationConditionOperator.GreaterThan => answers.Any(x => Compare(question.Type, x, expected) > 0),
            InformationConditionOperator.LessThan => answers.Any(x => Compare(question.Type, x, expected) < 0),
            InformationConditionOperator.GreaterThanOrEqual => answers.Any(x => Compare(question.Type, x, expected) >= 0),
            InformationConditionOperator.LessThanOrEqual => answers.Any(x => Compare(question.Type, x, expected) <= 0),
            _ => false,
        };
    }

    // Whether answers of this type have an order (the ordering operators apply).
    public static bool IsOrdered(this InformationItemType type) =>
        type is InformationItemType.WholeNumber or InformationItemType.Number or InformationItemType.Date or InformationItemType.DateTime;

    private static void Evaluate(
        IReadOnlyList<InformationItem> items,
        bool parentEnabled,
        IReadOnlyDictionary<string, InformationItem> questions,
        IReadOnlyDictionary<string, IReadOnlyList<string>> answers,
        HashSet<string> enabled)
    {
        foreach (var item in items)
        {
            var conditions = item.EnableWhen ?? [];

            bool Holds(InformationCondition condition) =>
                questions.TryGetValue(condition.Question, out var question) &&
                condition.Holds(question, enabled.Contains(question.Id) && answers.TryGetValue(question.Id, out var given) ? given : []);

            var isEnabled =
                parentEnabled &&
                (conditions.Count == 0 ||
                 (item.EnableBehavior == InformationEnableBehavior.Any ? conditions.Any(Holds) : conditions.All(Holds)));

            if (isEnabled)
            {
                enabled.Add(item.Id);
            }

            Evaluate(item.Items, isEnabled, questions, answers, enabled);
        }
    }

    // Ordering for numbers, dates and date-times; equality (0 or 1) for the other types; null when
    // either side cannot be read as the type (then no comparison holds).
    private static int? Compare(InformationItemType type, string answer, string expected) =>
        type switch
        {
            InformationItemType.Boolean =>
                bool.TryParse(answer, out var a) && bool.TryParse(expected, out var e) ? (a == e ? 0 : 1) : null,
            InformationItemType.WholeNumber or InformationItemType.Number =>
                decimal.TryParse(answer, NumberStyles.Number, CultureInfo.InvariantCulture, out var a) &&
                decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var e) ? a.CompareTo(e) : null,
            InformationItemType.Date =>
                DateOnly.TryParseExact(answer, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var a) &&
                DateOnly.TryParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e) ? a.CompareTo(e) : null,
            InformationItemType.DateTime =>
                DateTimeOffset.TryParse(answer, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var a) &&
                DateTimeOffset.TryParse(expected, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var e) ? a.CompareTo(e) : null,
            _ => string.Equals(answer.Trim(), expected.Trim(), StringComparison.Ordinal) ? 0 : 1,
        };

    private static bool IsQuestion(InformationItem item) =>
        item.Type is not (InformationItemType.Group or InformationItemType.Display);

    private static bool IsAnswered(IReadOnlyDictionary<string, IReadOnlyList<string>> answers, string id) =>
        answers.TryGetValue(id, out var given) && given.Any(x => !string.IsNullOrWhiteSpace(x));

    // Every item with the conditions it depends on: its own and its enclosing groups'.
    private static IEnumerable<(InformationItem Item, IReadOnlyList<InformationCondition> Conditions)> Flatten(
        IReadOnlyList<InformationItem> items,
        IReadOnlyList<InformationCondition> inherited)
    {
        foreach (var item in items)
        {
            IReadOnlyList<InformationCondition> conditions = [.. inherited, .. item.EnableWhen ?? []];

            yield return (item, conditions);

            foreach (var nested in Flatten(item.Items ?? [], conditions))
            {
                yield return nested;
            }
        }
    }
}
