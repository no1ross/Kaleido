using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Processor;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

// Guided lookups: a step input that takes its value from one query result is filled by the user,
// not the model. When the client shows forms and the agent calls the step without the lookup's
// inputs, the user searches, the query runs as the caller, the user picks one result, and the
// adapter fills the inputs from that row - the ids never pass through the model. The services
// still check what they receive (ADR 0008): continuation state is opaque but untrusted.
internal sealed partial class KaleidoToolInvoker
{
    private const string LookupInputKey = "kaleido-lookup";
    private const string LookupStatePrefix = "kaleido-mcp-lookup:";
    private const string SearchItemId = "search";
    private const string PickItemId = "result";
    private const string SearchAgainValue = "search-again";
    private const int LookupPageSize = 10;

    // The first lookup whose query the caller can run and whose inputs the agent did not pass.
    private static ToolLookup? MissingLookup(
        KaleidoTool tool,
        IReadOnlyDictionary<string, JsonElement> arguments) =>
        tool.Lookups.FirstOrDefault(lookup =>
            lookup.QueryTool is not null &&
            lookup.Inputs.Any(input =>
                !arguments.TryGetValue(input.Argument, out var value) ||
                value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()))));

    private async Task<ToolOutcome> GuideLookupAsync(
        KaleidoTool tool,
        ToolLookup lookup,
        KaleidoToolCall call,
        ToolCatalog catalog,
        Guid? processId,
        CancellationToken cancellationToken)
    {
        var state = new LookupState(
            tool.Definition.Name,
            lookup.Group,
            processId,
            JsonSerializer.Serialize(call.Arguments, KaleidoJsonOptions.Options),
            SearchText: null,
            Candidates: []);

        if (ElicitationMode(call) == Elicitation.Mrtr)
        {
            return AskLookup(SearchRequest(lookup, note: null), state);
        }

        string? note = null;

        // Session elicitation: search, pick, and search again while the user asks to.
        for (var attempt = 0; attempt < options.MaxElicitationRounds; attempt++)
        {
            var searched = await call.Server.ElicitAsync(elicitor.ToElicitation(SearchRequest(lookup, note)), cancellationToken);

            if (!searched.IsAccepted || Answer(SearchRequest(lookup, note), searched.Content, SearchItemId) is not { } searchText)
            {
                return LookupNotDone(tool, lookup, $"The user did not search ({searched.Action}). Nothing was run.");
            }

            var candidates = await SearchAsync(lookup, catalog, searchText, processId, cancellationToken);

            if (candidates.Rows.Count == 0)
            {
                note = $"No {lookup.QueryTitle ?? lookup.Source} result matched \"{searchText}\". Try other details.";
                continue;
            }

            var pickRequest = PickRequest(lookup, candidates);
            var picked = await call.Server.ElicitAsync(elicitor.ToElicitation(pickRequest), cancellationToken);

            if (!picked.IsAccepted)
            {
                return LookupNotDone(tool, lookup, $"The user did not pick a result ({picked.Action}). Nothing was run.");
            }

            if (Answer(pickRequest, picked.Content, PickItemId) is { } choice &&
                int.TryParse(choice, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) &&
                index >= 0 && index < candidates.Rows.Count)
            {
                return await RunStepAsync(tool, Filled(call.Arguments, lookup, candidates.Rows[index]), processId, call, catalog, cancellationToken);
            }

            note = null;
        }

        return LookupNotDone(tool, lookup, "The user did not find the record. Nothing was run.");
    }

    // The MRTR retry of a guided lookup: the user searched (show the results) or picked one (run the step).
    private async Task<ToolOutcome> ContinueLookupAsync(
        KaleidoToolCall call,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        if (DecodeLookupState(call.RequestState) is not { } state ||
            catalog.Find(state.Tool) is not { Kind: KaleidoToolKind.Step } tool ||
            tool.Lookups.FirstOrDefault(x => string.Equals(x.Group, state.Group, StringComparison.Ordinal)) is not { } lookup)
        {
            return Error(McpErrorCodes.InvalidArguments, "The lookup state is not readable; call the tool again.");
        }

        var arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(state.Arguments, KaleidoJsonOptions.Options) ?? [];

        ElicitResult? elicited = null;

        if (call.InputResponses?.TryGetValue(LookupInputKey, out var input) == true)
        {
            elicited = input.Deserialize(InputResponse.ElicitResultJsonTypeInfo);
        }

        if (elicited is not { IsAccepted: true, Content: { } content })
        {
            return LookupNotDone(tool, lookup, $"The user did not finish the lookup ({elicited?.Action ?? "no answer"}). Nothing was run.");
        }

        // Picked: fill the inputs from the chosen row and run the step.
        if (state.SearchText is not null)
        {
            var pickRequest = PickRequest(lookup, new LookupCandidates(state.Candidates.Select(x => x.Label).ToArray(), state.Candidates.Select(x => x.Values).ToArray(), null));

            if (Answer(pickRequest, content, PickItemId) is { } choice &&
                int.TryParse(choice, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index) &&
                index >= 0 && index < state.Candidates.Count)
            {
                var row = state.Candidates[index].Values;

                return await RunStepAsync(
                    tool,
                    Filled(arguments, lookup, row),
                    state.ProcessId,
                    call with { InputResponses = null, RequestState = null },
                    catalog,
                    cancellationToken);
            }

            return AskLookup(SearchRequest(lookup, note: null), state with { SearchText = null, Candidates = [] });
        }

        // Searched: show the results to pick from.
        if (Answer(SearchRequest(lookup, note: null), content, SearchItemId) is not { } searchText)
        {
            return LookupNotDone(tool, lookup, "The user did not enter anything to search for. Nothing was run.");
        }

        var candidates = await SearchAsync(lookup, catalog, searchText, state.ProcessId, cancellationToken);

        if (candidates.Rows.Count == 0)
        {
            return AskLookup(
                SearchRequest(lookup, $"No {lookup.QueryTitle ?? lookup.Source} result matched \"{searchText}\". Try other details."),
                state);
        }

        return AskLookup(
            PickRequest(lookup, candidates),
            state with
            {
                SearchText = searchText,
                Candidates = candidates.Labels.Select((label, i) => new LookupCandidate(label, candidates.Rows[i])).ToArray()
            });
    }

    // Runs the lookup's query as the caller; the step's process is passed when the query takes it
    // (searches scoped to the request).
    private async Task<LookupCandidates> SearchAsync(
        ToolLookup lookup,
        ToolCatalog catalog,
        string searchText,
        Guid? processId,
        CancellationToken cancellationToken)
    {
        var query = lookup.QueryTool is { } name ? catalog.Find(name) : null;

        JsonObject? parameters = null;

        if (processId is { } id && query is not null && TakesProcessId(query))
        {
            parameters = new JsonObject { ["processId"] = id.ToString() };
        }

        var request = new QueryApiRequest<JsonObject>(
            parameters,
            new QueryApiBody { SearchText = searchText, Page = new QueryApiPage { Size = LookupPageSize } });

        var client = Queryable(lookup.Service);

        var result = lookup.View is { } view
            ? await client.QueryViewAsync<JsonObject, JsonObject>(lookup.Source, view, request, cancellationToken)
            : await client.QuerySourceAsync<JsonObject, JsonObject>(lookup.Source, request, cancellationToken);

        var rows = result.Results
            .Select(row => (IReadOnlyDictionary<string, JsonElement>)lookup.Inputs.ToDictionary(
                input => input.Argument,
                input => row[Camel(input.Field)] is { } value ? JsonSerializer.SerializeToElement(value, KaleidoJsonOptions.Options) : default,
                StringComparer.Ordinal))
            .ToArray();

        var labels = result.Results.Select(row => Label(lookup, row)).ToArray();

        return new LookupCandidates(labels, rows, result.TotalCount > result.Results.Count ? result.TotalCount : null);
    }

    private static bool TakesProcessId(KaleidoTool query) =>
        query.Definition.InputSchema.TryGetProperty("properties", out var properties) &&
        properties.TryGetProperty("parameters", out var parameters) &&
        parameters.TryGetProperty("properties", out var fields) &&
        fields.TryGetProperty("processId", out _);

    // A result as the user recognises it: the lookup's display fields, in order.
    private static string Label(ToolLookup lookup, JsonObject row)
    {
        var fields = lookup.Display.Count > 0 ? lookup.Display : lookup.Inputs.Select(x => x.Field).ToArray();

        var parts = fields
            .Select(field => (Field: field, Value: row[Camel(field)]))
            .Select(x => x.Value is null
                ? null
                : x.Value.GetValueKind() switch
                {
                    JsonValueKind.True => Humanize(x.Field),
                    JsonValueKind.False => $"not {Humanize(x.Field).ToLowerInvariant()}",
                    _ => x.Value.ToString()
                })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OfType<string>();

        return string.Join(" · ", parts);
    }

    // "IsInNetwork" -> "In network".
    private static string Humanize(string field)
    {
        var name = field.StartsWith("Is", StringComparison.Ordinal) && field.Length > 2 && char.IsUpper(field[2]) ? field[2..] : field;
        var words = new StringBuilder();

        foreach (var c in name)
        {
            if (char.IsUpper(c) && words.Length > 0)
            {
                words.Append(' ');
            }

            words.Append(words.Length == 0 ? c : char.ToLowerInvariant(c));
        }

        return words.ToString();
    }

    private static InformationRequest SearchRequest(ToolLookup lookup, string? note) =>
        new()
        {
            InformationRequestId = $"lookup-search|{lookup.Group}",
            Title = $"Find {lookup.QueryTitle ?? lookup.Source}",
            Items =
            [
                new InformationItem
                {
                    Id = SearchItemId,
                    Text = (note is null ? string.Empty : note + " ") +
                        $"Search {lookup.QueryTitle ?? lookup.Source}: enter details that identify the record" +
                        (lookup.Display.Count > 0 ? $" (e.g. {string.Join(", ", lookup.Display.Take(3).Select(Humanize))})." : "."),
                    Type = InformationItemType.Text
                }
            ]
        };

    private static InformationRequest PickRequest(ToolLookup lookup, LookupCandidates candidates) =>
        new()
        {
            InformationRequestId = $"lookup-pick|{lookup.Group}",
            Title = $"Choose the {lookup.QueryTitle ?? lookup.Source} result",
            Items =
            [
                new InformationItem
                {
                    Id = PickItemId,
                    Text = candidates.More is { } total
                        ? $"Showing the first {candidates.Rows.Count} of {total} results. Pick one, or search again with more details."
                        : "Pick the result that is the right record, or search again.",
                    Type = InformationItemType.Choice,
                    Options = candidates.Labels
                        .Select((label, i) => new InformationOption { Value = i.ToString(System.Globalization.CultureInfo.InvariantCulture), Display = label })
                        .Append(new InformationOption { Value = SearchAgainValue, Display = "None of these: search again" })
                        .ToArray()
                }
            ]
        };

    private static Dictionary<string, JsonElement> Filled(
        IReadOnlyDictionary<string, JsonElement> arguments,
        ToolLookup lookup,
        IReadOnlyDictionary<string, JsonElement> row)
    {
        var filled = new Dictionary<string, JsonElement>(arguments, StringComparer.Ordinal);

        foreach (var (argument, value) in lookup.Inputs
                     .Select(input => (input.Argument, Found: row.TryGetValue(input.Argument, out var value), Value: value))
                     .Where(x => x.Found && x.Value.ValueKind is not JsonValueKind.Undefined)
                     .Select(x => (x.Argument, x.Value)))
        {
            filled[argument] = value;
        }

        return filled;
    }

    private string? Answer(
        InformationRequest request,
        IDictionary<string, JsonElement>? content,
        string itemId) =>
        content is null
            ? null
            : elicitor.ToAnswers(request, content.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal))
                .FirstOrDefault(x => string.Equals(x.ItemId, itemId, StringComparison.Ordinal))?
                .Answers.FirstOrDefault()?.Value is { Length: > 0 } value
                ? value
                : null;

    private ToolOutcome AskLookup(InformationRequest request, LookupState state) =>
        new()
        {
            InputRequired = new InputRequiredResult
            {
                InputRequests = new Dictionary<string, InputRequest>(StringComparer.Ordinal)
                {
                    [LookupInputKey] = InputRequest.ForElicitation(elicitor.ToElicitation(request))
                },
                RequestState = LookupStatePrefix + Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(state, KaleidoJsonOptions.Options))
            }
        };

    private static LookupState? DecodeLookupState(string? value)
    {
        if (value is null || !value.StartsWith(LookupStatePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LookupState>(
                Encoding.UTF8.GetString(Convert.FromBase64String(value[LookupStatePrefix.Length..])),
                KaleidoJsonOptions.Options);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    private static CallToolResult LookupNotDone(KaleidoTool tool, ToolLookup lookup, string note) =>
        Success(new JsonObject
        {
            ["step"] = tool.CapabilityName,
            ["outcome"] = "notRun",
            ["note"] = $"{note} {tool.Definition.Name} needs {string.Join(" and ", lookup.Inputs.Select(x => x.Argument))} from one {lookup.QueryTitle ?? lookup.Source} result."
        });

    // Opaque to the client but not trusted: the step's service checks the values it receives.
    private sealed record LookupState(
        string Tool,
        string Group,
        Guid? ProcessId,
        string Arguments,
        string? SearchText,
        IReadOnlyList<LookupCandidate> Candidates);

    private sealed record LookupCandidate(
        string Label,
        IReadOnlyDictionary<string, JsonElement> Values);

    private sealed record LookupCandidates(
        IReadOnlyList<string> Labels,
        IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> Rows,
        int? More);
}
