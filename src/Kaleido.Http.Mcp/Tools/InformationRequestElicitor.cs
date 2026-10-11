using System.Globalization;
using System.Text;
using System.Text.Json;
using Kaleido.Processor;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// Turns a Kaleido information request into an MCP elicitation, a form the client shows to the
/// <b>user</b>, and the user's form answers back into Kaleido answers. This is how questions reach
/// the person rather than the model: the text is shown verbatim and the agent cannot answer for
/// the user (ADR 0007).
/// </summary>
/// <remarks>
/// Elicitation forms are flat and primitive, so: <c>choice</c> → a single- or multi-select enum
/// (option values, with their display text as titles); <c>boolean</c> → a checkbox;
/// <c>wholeNumber</c>/<c>number</c> → a number; <c>date</c>/<c>dateTime</c> → a formatted string;
/// <c>text</c>/<c>longText</c> → a string. <c>group</c> headings and <c>display</c> text become part
/// of the message and of the question titles. Every question is required (Kaleido has no
/// optional questions). An item's proposed answers become the field's default: the form opens
/// pre-filled, and the user confirms or corrects it before submitting.
/// </remarks>
internal interface IInformationRequestElicitor
{
    ElicitRequestParams ToElicitation(InformationRequest request);

    /// <summary>Kaleido answers from accepted form content; questions are matched by item id.</summary>
    IReadOnlyList<InformationResponseItem> ToAnswers(
        InformationRequest request,
        IReadOnlyDictionary<string, JsonElement> content);
}

internal sealed class InformationRequestElicitor : IInformationRequestElicitor
{
    /// <summary>Shown with a form that opens pre-filled.</summary>
    public const string ProposedNote = "Some answers are pre-filled from what was said earlier. Check each one and correct it if needed before you submit.";

    public ElicitRequestParams ToElicitation(InformationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var message = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            message.AppendLine(request.Title.Trim());
        }

        var properties = new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>(StringComparer.Ordinal);
        var required = new List<string>();

        foreach (var (item, heading) in Questions(request.Items, heading: null, message))
        {
            properties[item.Id] = Schema(item, heading);
            required.Add(item.Id);
        }

        if (Questions(request.Items, heading: null, message: null).Any(x => x.Item.ProposedAnswers.Count > 0))
        {
            message.AppendLine().AppendLine(ProposedNote);
        }

        return new ElicitRequestParams
        {
            Message = message.Length > 0
                ? message.ToString().Trim()
                : "Please answer the following questions.",
            RequestedSchema = new ElicitRequestParams.RequestSchema
            {
                Properties = properties,
                Required = required,
            },
        };
    }

    public IReadOnlyList<InformationResponseItem> ToAnswers(
        InformationRequest request,
        IReadOnlyDictionary<string, JsonElement> content)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);

        var answers = new List<InformationResponseItem>();

        foreach (var (item, _) in Questions(request.Items, heading: null, message: null))
        {
            if (!content.TryGetValue(item.Id, out var value))
            {
                continue;
            }

            answers.Add(new InformationResponseItem
            {
                ItemId = item.Id,
                Answers = Values(value)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => new InformationAnswer { Value = x })
                    .ToArray(),
            });
        }

        return answers;
    }

    // Questions in order; group headings carry into their questions, display text into the message.
    private static IEnumerable<(InformationItem Item, string? Heading)> Questions(
        IReadOnlyList<InformationItem> items,
        string? heading,
        StringBuilder? message)
    {
        foreach (var item in items)
        {
            switch (item.Type)
            {
                case InformationItemType.Group:
                    message?.AppendLine().AppendLine(item.Text.Trim());

                    foreach (var nested in Questions(item.Items, item.Text.Trim(), message))
                    {
                        yield return nested;
                    }

                    break;

                case InformationItemType.Display:
                    message?.AppendLine(item.Text.Trim());
                    break;

                default:
                    yield return (item, heading);
                    break;
            }
        }
    }

    private static ElicitRequestParams.PrimitiveSchemaDefinition Schema(
        InformationItem item,
        string? heading)
    {
        var title = item.Text.Trim();
        var description = heading is null ? null : $"Part of: {heading}";
        var proposed = item.ProposedAnswers.Select(x => x.Value).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var first = proposed.FirstOrDefault();

        return item.Type switch
        {
            InformationItemType.Choice when item.Repeats =>
                new ElicitRequestParams.TitledMultiSelectEnumSchema
                {
                    Title = title,
                    Description = description,
                    MinItems = 1,
                    Items = new ElicitRequestParams.TitledEnumItemsSchema
                    {
                        AnyOf = item.Options.Select(Option).ToList(),
                    },
                    Default = proposed.Length > 0 ? proposed : null,
                },

            InformationItemType.Choice =>
                new ElicitRequestParams.TitledSingleSelectEnumSchema
                {
                    Title = title,
                    Description = description,
                    OneOf = item.Options.Select(Option).ToList(),
                    Default = first,
                },

            InformationItemType.Boolean =>
                new ElicitRequestParams.BooleanSchema
                {
                    Title = title,
                    Description = description,
                    Default = bool.TryParse(first, out var flag) ? flag : null,
                },

            InformationItemType.WholeNumber =>
                new ElicitRequestParams.NumberSchema { Type = "integer", Title = title, Description = description, Default = Number(first) },

            InformationItemType.Number =>
                new ElicitRequestParams.NumberSchema { Title = title, Description = description, Default = Number(first) },

            InformationItemType.Date =>
                new ElicitRequestParams.StringSchema { Title = title, Description = description, Format = "date", Default = first },

            InformationItemType.DateTime =>
                new ElicitRequestParams.StringSchema { Title = title, Description = description, Format = "date-time", Default = first },

            _ =>
                new ElicitRequestParams.StringSchema { Title = title, Description = description, MinLength = 1, Default = first },
        };
    }

    private static double? Number(string? value) =>
        double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static ElicitRequestParams.EnumSchemaOption Option(InformationOption option) =>
        new()
        {
            Const = option.Value,
            Title = string.IsNullOrWhiteSpace(option.Display) ? option.Value : option.Display,
        };

    // Form values back to Kaleido's invariant text: true/false, numbers with '.', text as given.
    private static IEnumerable<string> Values(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().SelectMany(Values),
            JsonValueKind.True => ["true"],
            JsonValueKind.False => ["false"],
            JsonValueKind.Number => [value.TryGetInt64(out var whole)
                ? whole.ToString(CultureInfo.InvariantCulture)
                : value.GetDecimal().ToString(CultureInfo.InvariantCulture)],
            JsonValueKind.String => [value.GetString() ?? string.Empty],
            _ => [],
        };
}
