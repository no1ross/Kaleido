using System.Globalization;

namespace Kaleido.Processor;

/// <summary>
/// Structural checks on information requests and the answers to them. Kaleido checks the
/// shape only, never the content: which questions are asked and what an answer means are the
/// implementer's concern.
/// </summary>
internal interface IInformationValidator
{
    /// <summary>Returns the problems with a request a handler returned; empty when well-formed.</summary>
    IReadOnlyCollection<StepProcessingMessage> ValidateRequest(
        InformationRequest request);

    /// <summary>Returns the problems with answers to <paramref name="request"/>; empty when they fit.</summary>
    IReadOnlyCollection<StepProcessingMessage> ValidateResponse(
        InformationRequest request,
        IInformationStep response);
}

internal sealed class InformationValidator : IInformationValidator
{
    public IReadOnlyCollection<StepProcessingMessage> ValidateRequest(
        InformationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<StepProcessingMessage>();

        if (string.IsNullOrWhiteSpace(request.InformationRequestId))
        {
            errors.Add(RequestError("The information request has no informationRequestId."));
        }

        if (request.Items.Count == 0)
        {
            errors.Add(RequestError($"Information request '{request.InformationRequestId}' has no items."));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var questions = request.Questions().GroupBy(x => x.Id, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var earlier = new Dictionary<string, InformationItem>(StringComparer.Ordinal);

        foreach (var item in Flatten(request.Items))
        {
            errors.AddRange(ValidateConditions(item, questions, earlier));

            if (item.Type is not (InformationItemType.Group or InformationItemType.Display) && !string.IsNullOrWhiteSpace(item.Id))
            {
                earlier.TryAdd(item.Id, item);
            }

            if (string.IsNullOrWhiteSpace(item.Id))
            {
                errors.Add(RequestError($"Information request '{request.InformationRequestId}' has an item without an id."));
                continue;
            }

            if (!ids.Add(item.Id))
            {
                errors.Add(RequestError($"Item id '{item.Id}' is used more than once in information request '{request.InformationRequestId}'."));
            }

            errors.AddRange(ValidateItem(item));
            errors.AddRange(ValidateProposedAnswers(item));
        }

        return errors;
    }

    // An item's text, and options or nested items as its type requires.
    private static IEnumerable<StepProcessingMessage> ValidateItem(InformationItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Text))
        {
            yield return RequestError($"Item '{item.Id}' has no text.");
        }

        switch (item.Type)
        {
            case InformationItemType.Choice when item.Options.Count == 0:
                yield return RequestError($"Choice item '{item.Id}' has no options.");
                break;

            case InformationItemType.Choice when !HasUniqueNonEmptyValues(item.Options):
                yield return RequestError($"Choice item '{item.Id}' has empty or duplicate option values.");
                break;

            case InformationItemType.Group when item.Items.Count == 0:
                yield return RequestError($"Group item '{item.Id}' has no nested items.");
                break;

            case not InformationItemType.Group when item.Items.Count > 0:
                yield return RequestError($"Item '{item.Id}' is a {item.Type} but has nested items; only groups nest.");
                break;
        }
    }

    public IReadOnlyCollection<StepProcessingMessage> ValidateResponse(
        InformationRequest request,
        IInformationStep response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        if (!string.Equals(
                response.InformationRequestId,
                request.InformationRequestId,
                StringComparison.Ordinal))
        {
            return
            [
                StepProcessingMessage.Error(
                    ProcessorErrorCodes.InformationResponseMismatch,
                    $"The answers are for information request '{response.InformationRequestId}', but the pending request is '{request.InformationRequestId}'."),
            ];
        }

        var errors = new List<StepProcessingMessage>();

        var questions =
            Flatten(request.Items)
                .ToDictionary(x => x.Id, StringComparer.Ordinal);

        var answered = MatchAnswers(request, response, questions, errors);

        // Only the questions the answers enable must be answered; the answers of a disabled one are
        // not counted when deciding what is enabled (ADR 0024).
        var enabled = request.EnabledItemIds(
            answered.ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<string>)[.. x.Value.Answers.Select(answer => answer?.Value).OfType<string>()],
                StringComparer.Ordinal));

        CheckAnswers(questions.Values, answered, enabled, errors);

        return errors;
    }

    // Each answer item matched to its question: unknown items, answers to groups or display items,
    // and items answered twice are errors.
    private static Dictionary<string, InformationResponseItem> MatchAnswers(
        InformationRequest request,
        IInformationStep response,
        Dictionary<string, InformationItem> questions,
        List<StepProcessingMessage> errors)
    {
        var answered =
            new Dictionary<string, InformationResponseItem>(StringComparer.Ordinal);

        foreach (var item in Flatten(response.Items ?? []))
        {
            if (!questions.TryGetValue(item.ItemId ?? string.Empty, out var question))
            {
                errors.Add(AnswerError($"Item '{item.ItemId}' is not part of information request '{request.InformationRequestId}'."));
                continue;
            }

            if (question.Type is InformationItemType.Group or InformationItemType.Display)
            {
                if (item.Answers.Count > 0)
                {
                    errors.Add(AnswerError($"Item '{question.Id}' is a {question.Type} and takes no answer."));
                }

                continue;
            }

            if (!answered.TryAdd(question.Id, item))
            {
                errors.Add(AnswerError($"Item '{question.Id}' is answered more than once."));
            }
        }

        return answered;
    }

    // Every enabled question has an answer, as many as it takes, each fitting the question's type; a
    // disabled question takes none.
    private static void CheckAnswers(
        IEnumerable<InformationItem> questions,
        Dictionary<string, InformationResponseItem> answered,
        IReadOnlySet<string> enabled,
        List<StepProcessingMessage> errors)
    {
        foreach (var question in questions)
        {
            if (question.Type is InformationItemType.Group or InformationItemType.Display)
            {
                continue;
            }

            if (!enabled.Contains(question.Id))
            {
                if (answered.TryGetValue(question.Id, out var disabled) && disabled.Answers.Count > 0)
                {
                    errors.Add(AnswerError($"Question '{question.Id}' does not apply to the answers given and takes no answer."));
                }

                continue;
            }

            if (!answered.TryGetValue(question.Id, out var item) || item.Answers.Count == 0)
            {
                errors.Add(
                    StepProcessingMessage.Error(
                        ProcessorErrorCodes.InformationResponseUnanswered,
                        $"Question '{question.Id}' has no answer."));

                continue;
            }

            if (item.Answers.Count > 1 && !question.Repeats)
            {
                errors.Add(AnswerError($"Question '{question.Id}' takes one answer but received {item.Answers.Count}."));
            }

            foreach (var answer in item.Answers.Where(answer => !Fits(question, answer?.Value)))
            {
                errors.Add(AnswerError($"Answer '{answer?.Value}' does not fit question '{question.Id}' ({Describe(question)})."));
            }
        }
    }

    // A condition tests an earlier question (so conditions never form a cycle), with an operator its
    // type supports and a value that fits it.
    private static IEnumerable<StepProcessingMessage> ValidateConditions(
        InformationItem item,
        Dictionary<string, InformationItem> questions,
        Dictionary<string, InformationItem> earlier)
    {
        foreach (var condition in item.EnableWhen ?? [])
        {
            if (condition is null || string.IsNullOrWhiteSpace(condition.Question))
            {
                yield return RequestError($"Item '{item.Id}' has an enableWhen condition without a question.");
                continue;
            }

            if (!earlier.TryGetValue(condition.Question, out var question))
            {
                yield return RequestError(questions.ContainsKey(condition.Question) && !string.Equals(condition.Question, item.Id, StringComparison.Ordinal)
                    ? $"Item '{item.Id}' has a condition on question '{condition.Question}', which comes after it; a condition tests an earlier question."
                    : $"Item '{item.Id}' has a condition on '{condition.Question}', which is not an earlier question of the request.");
                continue;
            }

            if (!Enum.IsDefined(condition.Operator))
            {
                yield return RequestError($"Item '{item.Id}' has a condition with an unknown operator.");
            }
            else if (condition.Operator == InformationConditionOperator.Exists)
            {
                if (!bool.TryParse(condition.Answer, out _))
                {
                    yield return RequestError($"Item '{item.Id}' has an exists condition on '{question.Id}' whose answer is not true or false.");
                }
            }
            else if (condition.Operator is not (InformationConditionOperator.Equals or InformationConditionOperator.NotEquals) && !question.Type.IsOrdered())
            {
                yield return RequestError($"Item '{item.Id}' compares '{question.Id}' with {condition.Operator}, but a {question.Type} answer has no order; use Equals, NotEquals or Exists.");
            }
            else if (!Fits(question, condition.Answer))
            {
                yield return RequestError($"Item '{item.Id}' has a condition on '{question.Id}' whose answer '{condition.Answer}' does not fit the question ({Describe(question)}).");
            }
        }

        if (!Enum.IsDefined(item.EnableBehavior))
        {
            yield return RequestError($"Item '{item.Id}' has an unknown enableBehavior.");
        }
    }

    // A proposal must be something the user could submit unchanged.
    private static IEnumerable<StepProcessingMessage> ValidateProposedAnswers(
        InformationItem item)
    {
        var proposed = item.ProposedAnswers ?? [];

        if (proposed.Count == 0)
        {
            yield break;
        }

        if (item.Type is InformationItemType.Group or InformationItemType.Display)
        {
            yield return RequestError($"Item '{item.Id}' is a {item.Type} and takes no proposed answer.");
            yield break;
        }

        if (proposed.Count > 1 && !item.Repeats)
        {
            yield return RequestError($"Question '{item.Id}' takes one answer but proposes {proposed.Count}.");
        }

        foreach (var answer in proposed.Where(answer => !Fits(item, answer?.Value)))
        {
            yield return RequestError($"Proposed answer '{answer?.Value}' does not fit question '{item.Id}' ({Describe(item)}).");
        }
    }

    // One table of what each item type accepts; the shape only, never the meaning.
    private static bool Fits(
        InformationItem question,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return question.Type switch
        {
            InformationItemType.Text or InformationItemType.LongText => true,
            InformationItemType.Boolean => bool.TryParse(value, out _),
            InformationItemType.WholeNumber => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
            InformationItemType.Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _),
            InformationItemType.Date => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            InformationItemType.DateTime => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
            InformationItemType.Choice => question.Options.Any(x => string.Equals(x.Value, value, StringComparison.Ordinal)),
            _ => false,
        };
    }

    private static string Describe(
        InformationItem question) =>
        question.Type switch
        {
            InformationItemType.Boolean => "expects true or false",
            InformationItemType.WholeNumber => "expects a whole number",
            InformationItemType.Number => "expects a number",
            InformationItemType.Date => "expects a date, yyyy-MM-dd",
            InformationItemType.DateTime => "expects an ISO 8601 date and time",
            InformationItemType.Choice => $"expects one of: {string.Join(", ", question.Options.Select(x => x.Value))}",
            _ => "expects non-empty text",
        };

    private static IEnumerable<InformationItem> Flatten(
        IEnumerable<InformationItem> items) =>
        items.SelectMany(x => new[] { x }.Concat(Flatten(x.Items)));

    private static IEnumerable<InformationResponseItem> Flatten(
        IEnumerable<InformationResponseItem> items) =>
        items.SelectMany(x => new[] { x }.Concat(Flatten(x.Items)));

    private static StepProcessingMessage RequestError(
        string message) =>
        StepProcessingMessage.Error(
            ProcessorErrorCodes.InformationRequestInvalid,
            message);

    private static StepProcessingMessage AnswerError(
        string message) =>
        StepProcessingMessage.Error(
            ProcessorErrorCodes.InformationResponseInvalidAnswer,
            message);

    // Every option value is set and used once (ordinal: values are machine identifiers).
    private static bool HasUniqueNonEmptyValues(IReadOnlyList<InformationOption> options)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        return options.All(x => !string.IsNullOrWhiteSpace(x.Value) && seen.Add(x.Value));
    }
}
