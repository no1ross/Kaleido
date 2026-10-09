using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.Http;
using Kaleido.Http.Mcp.Observability;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// The MCP server's tool hooks: <c>tools/list</c> returns the caller's tools, translated from the
/// registry the caller may see; <c>tools/call</c> runs the named tool against Kaleido. The tools
/// are not fixed at startup, they follow the registry and the caller's access.
/// </summary>
/// <remarks>
/// Every request gets a well-formed, successful answer, never a protocol error: a tool result
/// (problems are results with <c>isError: true</c> and an <c>mcp_*</c> code), the "input required"
/// result when the user must answer questions, or an empty tool list when Kaleido cannot be
/// reached. Only the caller's own cancellation propagates (nobody is waiting). Every tool result
/// carries the call's <c>requestId</c>, the same <c>X-Kaleido-Request-Id</c> sent to Kaleido.
/// </remarks>
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
    IKaleidoToolInvoker invoker,
    IMcpObservability observability,
    IMcpRequestIdAccessor requestIdAccessor,
    ILogger<KaleidoMcpToolHandler> logger)
    : IKaleidoMcpToolHandler
{
    public async ValueTask<ListToolsResult> ListToolsAsync(
        RequestContext<ListToolsRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        using var observation = observability.BeginToolsList();

        try
        {
            var catalog = await catalogProvider.GetAsync(cancellationToken);

            observation.Completed(catalog);

            return new ListToolsResult
            {
                Tools = catalog.Tools.Select(x => x.Definition).ToList()
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Kaleido MCP: listing tools failed unexpectedly; returning no tools.");

            return new ListToolsResult { Tools = [] };
        }
    }

    public async ValueTask<CallToolResult> CallToolAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Params?.Name ?? string.Empty;

        using var observation = observability.BeginToolCall(name);

        try
        {
            var outcome = await RunAsync(request, name, observation, cancellationToken);

            // The SDK's stable MRTR contract: "input required" is signalled by this exception,
            // which the SDK answers as a normal successful input-required result (never an error on
            // the wire). This is the only throw in the adapter; everything below returns outcomes.
            if (outcome.InputRequired is { } inputRequired)
            {
                observation.InputRequired();

                throw new InputRequiredException(inputRequired);
            }

            var result = WithRequestId(
                outcome.Result ?? Error(McpErrorCodes.InternalError, $"{name} returned no result."));

            observation.Completed(result);

            return result;
        }
        catch (OperationCanceledException)
        {
            observation.Canceled();

            throw;
        }
    }

    private async Task<ToolOutcome> RunAsync(
        RequestContext<CallToolRequestParams> request,
        string name,
        IMcpToolCallObservation observation,
        CancellationToken cancellationToken)
    {
        ToolCatalog catalog;

        try
        {
            catalog = await catalogProvider.GetAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Kaleido MCP: reading the tools failed unexpectedly for {Tool}.", name);

            return Error(McpErrorCodes.InternalError, $"{name} did not run: the tools could not be read.");
        }

        if (catalog.Unavailable is { } reason)
        {
            return Error(McpErrorCodes.ServiceUnavailable, $"{name} did not run: Kaleido is unavailable. {reason}");
        }

        if (catalog.Find(name) is not { } tool)
        {
            return Error(
                McpErrorCodes.UnknownTool,
                $"There is no tool '{name}' for you. Tools follow your access and the deployed services; list the tools again.");
        }

        observation.Resolved(tool);

        var arguments =
            request.Params?.Arguments is { } given
                ? new Dictionary<string, JsonElement>(given, StringComparer.Ordinal)
                : new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return await invoker.InvokeAsync(
            tool,
            new KaleidoToolCall
            {
                Arguments = arguments,
                Server = request.Server,
                Meta = request.Params?.Meta,
                InputResponses = request.Params?.InputResponses is { } responses
                    ? new Dictionary<string, InputResponse>(responses, StringComparer.Ordinal)
                    : null,
                RequestState = request.Params?.RequestState
            },
            catalog,
            cancellationToken);
    }

    // The request id goes into the result the agent reads, so a user can quote it and an operator
    // can find the call in every service's logs and traces.
    private CallToolResult WithRequestId(CallToolResult result)
    {
        var requestId = requestIdAccessor.Current;

        foreach (var block in result.Content.OfType<TextContentBlock>())
        {
            if (!block.Text.TrimStart().StartsWith('{'))
            {
                continue;
            }

            try
            {
                if (JsonNode.Parse(block.Text) is JsonObject body)
                {
                    body["requestId"] = requestId;
                    block.Text = body.ToJsonString(KaleidoJsonOptions.Options);
                }
            }
            catch (JsonException)
            {
                // Not JSON after all: leave the text as it is.
            }
        }

        return result;
    }

    private static CallToolResult Error(string code, string message) =>
        new()
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = new JsonObject { ["code"] = code, ["message"] = message }.ToJsonString(KaleidoJsonOptions.Options)
                }
            ]
        };
}
