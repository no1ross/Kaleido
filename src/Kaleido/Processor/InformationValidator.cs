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

        foreach (var item in Flatten(request.Items))
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                errors.Add(RequestError($"Information request '{request.InformationRequestId}' has an item without an id."));
                continue;
            }

            if (!ids.Add(item.Id))
            {
                errors.Add(RequestError($"Item id '{item.Id}' is used more than once in information request '{request.InformationRequestId}'."));
            }

            if (string.IsNullOrWhiteSpace(item.Text))
            {
                errors.Add(RequestError($"Item '{item.Id}' has no text."));
            }

            switch (item.Type)
            {
                case InformationItemType.Choice when item.Options.Count == 0:
                    errors.Add(RequestError($"Choice item '{item.Id}' has no options."));
                    break;

                case InformationItemType.Choice
                    when item.Options.Select(x => x.Value).Distinct(StringComparer.Ordinal).Count() != item.Options.Count
                        || item.Options.Any(x => string.IsNullOrWhiteSpace(x.Value)):
                    errors.Add(RequestError($"Choice item '{item.Id}' has empty or duplicate option values."));
                    break;

                case InformationItemType.Group when item.Items.Count == 0:
                    errors.Add(RequestError($"Group item '{item.Id}' has no nested items."));
                    break;

                case not InformationItemType.Group when item.Items.Count > 0:
                    errors.Add(RequestError($"Item '{item.Id}' is a {item.Type} but has nested items; only groups nest."));
                    break;
            }
        }

        return errors;
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
                    $"The answers are for information request '{response.InformationRequestId}', but the pending request is '{request.InformationRequestId}'.")
            ];
        }

        var errors = new List<StepProcessingMessage>();

        var questions =
            Flatten(request.Items)
                .ToDictionary(x => x.Id, StringComparer.Ordinal);

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

        foreach (var question in questions.Values)
        {
            if (question.Type is InformationItemType.Group or InformationItemType.Display)
            {
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

        return errors;
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
            _ => false
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
            _ => "expects non-empty text"
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
}
