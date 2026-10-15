using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

// The front door (McpToolListing.FrontDoor): every tool is still built, only a small menu is listed.
// The menu is derived from what the registry already publishes, so it never needs maintaining: the
// steps that can start a process, the queries those steps declare as lookups ([Lookup]), the
// host's configured extra names, and the built-ins. Everything else is reached through next,
// search_tools and call_tool.
internal sealed partial class ToolCatalogBuilder
{
    // Marks the front door listed and the rest unlisted; returns the configured names it did not find.
    private List<string> ListFrontDoor(BuildContext context)
    {
        AddFrontDoorTools(context);

        var listed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tool in context.Tools)
        {
            if (tool.Kind is not (KaleidoToolKind.Query or KaleidoToolKind.Step))
            {
                listed.Add(tool.Definition.Name);
            }
            else if (tool is { Kind: KaleidoToolKind.Step, IsInitial: true })
            {
                listed.Add(tool.Definition.Name);
                listed.UnionWith(tool.Lookups.Select(x => x.QueryTool).OfType<string>());
            }
        }

        var unknown = new List<string>();

        foreach (var name in options.ListedTools)
        {
            if (context.Names.Contains(name))
            {
                listed.Add(name);
            }
            else
            {
                unknown.Add(name);
            }
        }

        for (var i = 0; i < context.Tools.Count; i++)
        {
            context.Tools[i] = context.Tools[i] with { Listed = listed.Contains(context.Tools[i].Definition.Name) };
        }

        return unknown;
    }

    private static void AddFrontDoorTools(BuildContext context)
    {
        context.Add(SearchToolsDefinition());
        context.Add(DescribeToolDefinition());
        context.Add(CallToolDefinition());
    }

    private static JsonObject NameSchema() =>
        new()
        {
            ["type"] = "string",
            ["description"] = "The tool's name, as given in next, search_tools or describe_tool.",
        };

    private static KaleidoTool SearchToolsDefinition() =>
        new()
        {
            Definition = new Tool
            {
                Name = SearchToolsTool,
                Title = "Search tools",
                Description =
                    "Finds tools that are not on your tool list by what they do (e.g. \"previous requests\", \"zip code\"). " +
                    "Returns names, descriptions and required inputs; read one tool's full inputs with describe_tool and run it with call_tool. " +
                    "Steps that are open in a process are already described in next. Read-only.",
                InputSchema = JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["text"] = new JsonObject { ["type"] = "string", ["description"] = "What you are looking for, in a few words." },
                        ["kind"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("query", "step"), ["description"] = "Only searches (query) or only steps (step)." },
                    },
                    ["required"] = new JsonArray("text"),
                }),
                Annotations = new ToolAnnotations { ReadOnlyHint = true, IdempotentHint = true, OpenWorldHint = false },
            },
            Kind = KaleidoToolKind.SearchTools,
        };

    private static KaleidoTool DescribeToolDefinition() =>
        new()
        {
            Definition = new Tool
            {
                Name = DescribeToolTool,
                Title = "Describe tool",
                Description = "Returns one tool's full description, inputs and hints (whether it changes state or cannot be undone, and the searches its inputs come from). Read-only.",
                InputSchema = JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject { ["name"] = NameSchema() },
                    ["required"] = new JsonArray("name"),
                }),
                Annotations = new ToolAnnotations { ReadOnlyHint = true, IdempotentHint = true, OpenWorldHint = false },
            },
            Kind = KaleidoToolKind.DescribeTool,
        };

    private static KaleidoTool CallToolDefinition() =>
        new()
        {
            Definition = new Tool
            {
                Name = CallToolTool,
                Title = "Call tool",
                Description =
                    "Runs a tool that is not on your tool list: a step or search described in next, or found with search_tools. " +
                    "Pass its name and the same arguments the tool takes. It runs exactly as the tool itself, with your access and the process rules. " +
                    "A step changes state and may not be undoable: confirm with the user before running one.",
                InputSchema = JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["name"] = NameSchema(),
                        ["arguments"] = new JsonObject { ["type"] = "object", ["description"] = "The tool's arguments (see its inputs in next or describe_tool), processId included." },
                    },
                    ["required"] = new JsonArray("name"),
                }),

                // It can run any step, so clients keep asking the user before each call.
                Annotations = new ToolAnnotations { ReadOnlyHint = false, DestructiveHint = true, IdempotentHint = false, OpenWorldHint = false },
            },
            Kind = KaleidoToolKind.CallTool,
        };
}
