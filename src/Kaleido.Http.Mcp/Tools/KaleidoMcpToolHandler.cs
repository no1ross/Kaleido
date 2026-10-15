using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Client;
using Kaleido.Http.Mcp.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
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
/// result when the user must answer questions, or an empty tool list when the tools cannot be
/// read. Only the caller's own cancellation propagates (nobody is waiting). Every tool result
/// carries the call's <c>requestId</c>, the same <c>X-Kaleido-Request-Id</c> sent to Kaleido.
/// Tools run as the caller: the calls they make carry the caller's own <c>Authorization</c>,
/// whatever credentials the host configured on its Kaleido clients.
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

internal sealed partial class KaleidoMcpToolHandler(
    IToolCatalogProvider catalogProvider,
    IKaleidoToolInvoker invoker,
    IMcpObservability observability,
    IKaleidoCorrelationContextAccessor correlation,
    IKaleidoCallerForwarding callerForwarding,
    IHttpContextAccessor httpContextAccessor,
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
                Tools = catalog.Tools.Where(x => x.Listed).Select(x => x.Definition).ToList(),
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogListToolsFailed(logger, exception);

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
            LogReadToolsFailed(logger, exception, name);

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

        var arguments =
            request.Params?.Arguments is { } given
                ? new Dictionary<string, JsonElement>(given, StringComparer.Ordinal)
                : new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        // call_tool runs another of the caller's tools, listed or not, exactly as if it were called.
        if (tool.Kind == KaleidoToolKind.CallTool)
        {
            if (Unwrap(catalog, arguments) is not var (inner, innerArguments))
            {
                return Error(McpErrorCodes.UnknownTool, $"{name}: pass the name of one of your tools (from next, search_tools or describe_tool) and its arguments.");
            }

            (tool, arguments) = (inner, innerArguments);
        }

        observation.Resolved(tool, ProcessIdOf(arguments));

        return await InvokeAsCallerAsync(request, tool, arguments, catalog, cancellationToken);
    }

    private async Task<ToolOutcome> InvokeAsCallerAsync(
        RequestContext<CallToolRequestParams> request,
        KaleidoTool tool,
        Dictionary<string, JsonElement> arguments,
        ToolCatalog catalog,
        CancellationToken cancellationToken)
    {
        // A front door calls services as the caller, never with the host's own credentials.
        using var asCaller = callerForwarding.ForwardCaller(
            httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString());

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
                RequestState = request.Params?.RequestState,
            },
            catalog,
            cancellationToken);
    }

    // The request id goes into the result the agent reads, so a user can quote it and an operator
    // can find the call in every service's logs and traces.
    private CallToolResult WithRequestId(CallToolResult result)
    {
        var requestId = correlation.Current.RequestId;

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

    // call_tool's target and arguments; null when the name is not a tool of this caller (call_tool
    // itself included: one level only).
    private static (KaleidoTool Tool, Dictionary<string, JsonElement> Arguments)? Unwrap(
        ToolCatalog catalog,
        Dictionary<string, JsonElement> arguments)
    {
        if (!arguments.TryGetValue("name", out var name) ||
            name.ValueKind != JsonValueKind.String ||
            catalog.Find(name.GetString() ?? string.Empty) is not { } inner ||
            inner.Kind == KaleidoToolKind.CallTool)
        {
            return null;
        }

        var innerArguments =
            arguments.TryGetValue("arguments", out var given) && given.ValueKind == JsonValueKind.Object
                ? given.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone(), StringComparer.Ordinal)
                : new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        return (inner, innerArguments);
    }

    // The call's processId argument when it is a valid id (tagging only; the invoker validates it).
    private static string? ProcessIdOf(Dictionary<string, JsonElement> arguments) =>
        arguments.TryGetValue(ToolCatalogBuilder.ProcessIdArgument, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        Guid.TryParse(value.GetString(), out var id)
            ? id.ToString()
            : null;

    private static CallToolResult Error(string code, string message) =>
        new()
        {
            IsError = true,
            Content =
            [
                new TextContentBlock
                {
                    Text = new JsonObject { ["code"] = code, ["message"] = message }.ToJsonString(KaleidoJsonOptions.Options),
                },
            ],
        };

    [LoggerMessage(Level = LogLevel.Error, Message = "Kaleido MCP: listing tools failed unexpectedly; returning no tools.")]
    private static partial void LogListToolsFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Kaleido MCP: reading the tools failed unexpectedly for {Tool}.")]
    private static partial void LogReadToolsFailed(ILogger logger, Exception exception, string tool);
}
