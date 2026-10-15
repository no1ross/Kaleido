using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

// The front door's side of a tool call: search_tools and describe_tool answer from the caller's
// catalog (no service call), and next describes every open step that is not on the tool list in
// full - its inputs, approval hints and the searches its inputs come from, with the processId
// already in their arguments - so the agent can run it with call_tool without looking anything up.
internal sealed partial class KaleidoToolInvoker
{
    private const int MaxSearchResults = 10;

    private const string FrontDoorHowToRun =
        "Steps and searches that are not on your tool list are described here: run them with call_tool (name, arguments). Confirm with the user before running a step.";

    private static readonly char[] SearchSeparators = [' ', '_', '-', ',', '.', '/'];

    // An open step's (or its search's) full description, for a tool that is not listed.
    private static JsonObject WithDetails(JsonObject entry, KaleidoTool tool, ToolCatalog catalog, Guid? processId)
    {
        if (!catalog.IsFrontDoor || tool.Listed)
        {
            return entry;
        }

        foreach (var (key, value) in Details(tool, catalog, processId))
        {
            entry[key] = value?.DeepClone();
        }

        return entry;
    }

    private static bool HasDetails(JsonObject next, JsonNode[] available) =>
        next["required"]?["inputs"] is not null ||
        available.Any(x => x["inputs"] is not null);

    private static JsonObject Details(KaleidoTool tool, ToolCatalog catalog, Guid? processId)
    {
        var details = new JsonObject
        {
            ["description"] = tool.Definition.Description,
            ["inputs"] = JsonNode.Parse(tool.Definition.InputSchema.GetRawText()),
            ["hints"] = Hints(tool.Definition.Annotations),
        };

        var lookups = tool.Lookups.Select(x => (JsonNode?)Lookup(x, catalog, processId)).ToArray();

        if (lookups.Length > 0)
        {
            details["lookups"] = new JsonArray(lookups);
        }

        return details;
    }

    private static JsonObject Hints(ToolAnnotations? annotations) =>
        new()
        {
            ["readOnly"] = annotations?.ReadOnlyHint ?? false,
            ["destructive"] = annotations?.DestructiveHint ?? false,
            ["idempotent"] = annotations?.IdempotentHint ?? false,
        };

    // Which inputs a search fills, and how to run that search (the step's processId pre-filled).
    private static JsonObject Lookup(ToolLookup lookup, ToolCatalog catalog, Guid? processId)
    {
        var fills = new JsonObject();

        foreach (var (argument, field) in lookup.Inputs)
        {
            fills[argument] = field;
        }

        if (lookup.QueryTool is not { } name || catalog.Find(name) is not { } query)
        {
            return new JsonObject
            {
                ["fills"] = fills,
                ["note"] = $"These come from {lookup.View ?? lookup.Source} on {lookup.Service}, which you cannot search: ask the user.",
            };
        }

        var search = new JsonObject
        {
            ["tool"] = name,
            ["inputs"] = JsonNode.Parse(query.Definition.InputSchema.GetRawText()),
        };

        if (processId is { } id)
        {
            search["arguments"] = new JsonObject { [ToolCatalogBuilder.ProcessIdArgument] = id.ToString() };
        }

        if (lookup.Display.Count > 0)
        {
            search["show"] = new JsonArray([.. lookup.Display.Select(x => (JsonNode?)x)]);
        }

        return new JsonObject { ["fills"] = fills, ["search"] = search };
    }

    // search_tools: the caller's tools ranked by how many of the words they mention.
    private static CallToolResult SearchTools(IReadOnlyDictionary<string, JsonElement> arguments, ToolCatalog catalog)
    {
        if (Text(arguments, "text") is not { } text)
        {
            return Error(McpErrorCodes.InvalidArguments, $"{ToolCatalogBuilder.SearchToolsTool}: text is required: what you are looking for, in a few words.");
        }

        var kind = Text(arguments, "kind");
        var words = text.ToLowerInvariant().Split(SearchSeparators, StringSplitOptions.RemoveEmptyEntries);

        var matches = catalog.Tools
            .Where(x =>
                x.Kind is KaleidoToolKind.Query or KaleidoToolKind.Step &&
                (kind is null || string.Equals(kind, x.Kind == KaleidoToolKind.Query ? "query" : "step", StringComparison.OrdinalIgnoreCase)))
            .Select(x => (Tool: x, Score: Score(x, words)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Tool.Definition.Name, StringComparer.Ordinal)
            .Take(MaxSearchResults)
            .Select(x => (JsonNode?)new JsonObject
            {
                ["name"] = x.Tool.Definition.Name,
                ["title"] = x.Tool.Definition.Title,
                ["kind"] = x.Tool.Kind == KaleidoToolKind.Query ? "query" : "step",
                ["description"] = x.Tool.Definition.Description,
                ["required"] = new JsonArray([.. RequiredNames(x.Tool.Definition.InputSchema).Select(r => (JsonNode?)r)]),
                ["listed"] = x.Tool.Listed,
            })
            .ToArray();

        return Success(new JsonObject
        {
            ["tools"] = new JsonArray(matches),
            ["note"] = matches.Length == 0
                ? "No tool matched. Try other words, or check next for the steps that are open now."
                : $"Read a tool's full inputs with {ToolCatalogBuilder.DescribeToolTool}; run one that is not listed with {ToolCatalogBuilder.CallToolTool}.",
        });
    }

    private static int Score(KaleidoTool tool, string[] words)
    {
        var haystack = string.Join(' ', tool.Definition.Name, tool.Definition.Title, tool.Definition.Description, tool.Service).ToLowerInvariant();

        return words.Count(word => haystack.Contains(word, StringComparison.Ordinal));
    }

    // describe_tool: one tool in full, as next would describe it (without a process).
    private static CallToolResult DescribeTool(IReadOnlyDictionary<string, JsonElement> arguments, ToolCatalog catalog)
    {
        if (Text(arguments, "name") is not { } name || catalog.Find(name) is not { } tool)
        {
            return Error(
                McpErrorCodes.UnknownTool,
                $"{ToolCatalogBuilder.DescribeToolTool}: there is no tool '{Text(arguments, "name")}' for you. Find tools with {ToolCatalogBuilder.SearchToolsTool}.");
        }

        var body = new JsonObject
        {
            ["name"] = tool.Definition.Name,
            ["title"] = tool.Definition.Title,
            ["listed"] = tool.Listed,
        };

        foreach (var (key, value) in Details(tool, catalog, processId: null))
        {
            body[key] = value?.DeepClone();
        }

        if (!tool.Listed)
        {
            body["howToRun"] = $"Not on your tool list: run it with {ToolCatalogBuilder.CallToolTool} (name: \"{tool.Definition.Name}\", arguments).";
        }

        return Success(body);
    }
}
