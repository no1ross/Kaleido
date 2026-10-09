using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// The MCP server's tool hooks: <c>tools/list</c> returns the caller's tools, translated from the
/// registry the caller may see; <c>tools/call</c> runs the named tool against Kaleido. The tools
/// are not fixed at startup, they follow the registry and the caller's access.
/// </summary>
internal interface IKaleidoMcpToolHandler
{
    ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken = default);

    ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default);
}

internal sealed class KaleidoMcpToolHandler(
    IToolCatalogProvider catalogProvider,
    IKaleidoToolInvoker invoker)
    : IKaleidoMcpToolHandler
{
    public async ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        var catalog = await catalogProvider.GetAsync(cancellationToken);

        return new ListToolsResult
        {
            Tools = catalog.Tools.Select(x => x.Definition).ToList()
        };
    }

    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Params?.Name ?? string.Empty;
        var catalog = await catalogProvider.GetAsync(cancellationToken);

        if (catalog.Find(name) is not { } tool)
        {
            var body = new JsonObject
            {
                ["code"] = McpErrorCodes.UnknownTool,
                ["message"] = $"There is no tool '{name}' for you. Tools follow your access and the deployed services; list the tools again."
            };

            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = body.ToJsonString(KaleidoJsonOptions.Options) }]
            };
        }

        var arguments =
            request.Params?.Arguments is { } given
                ? new Dictionary<string, JsonElement>(given, StringComparer.Ordinal)
                : new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return await invoker.InvokeAsync(tool, arguments, catalog, request.Server, cancellationToken);
    }
}
