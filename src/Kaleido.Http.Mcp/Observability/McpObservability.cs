using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.Tools;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Observability;

/// <summary>
/// The adapter's traces, metrics and boundary logs. One signal per event at the level that owns it:
/// a tool call is observed once (span, counter, duration, one Information log), a listing once, a
/// registry read once. A caller's cancellation is recorded as <c>canceled</c>, never as a failure.
/// </summary>
internal interface IMcpObservability
{
    IMcpToolCallObservation BeginToolCall(string toolName);

    IMcpToolsListObservation BeginToolsList();

    void RegistryRead(string result, string? revision);
}

internal interface IMcpToolCallObservation : IDisposable
{
    /// <summary>The tool was found: tag its kind, service and capability.</summary>
    void Resolved(KaleidoTool tool);

    /// <summary>The call finished with a result (successful, or <c>isError</c> with an <c>mcp_*</c> code).</summary>
    void Completed(CallToolResult result);

    /// <summary>The call returned "input required": the user is being asked questions.</summary>
    void InputRequired();

    /// <summary>The caller cancelled; nobody is waiting for the result.</summary>
    void Canceled();
}

internal interface IMcpToolsListObservation : IDisposable
{
    void Completed(ToolCatalog catalog);
}

internal sealed class McpObservability(
    IKaleidoCorrelationContextAccessor correlation,
    ILogger<McpObservability> logger)
    : IMcpObservability
{
    private static readonly ActivitySource ActivitySource =
        new(McpTelemetry.ActivitySourceName);

    private static readonly Meter Meter =
        new(McpTelemetry.MeterName);

    private static readonly Counter<long> ToolCallsCounter =
        Meter.CreateCounter<long>(
            McpTelemetry.ToolCallsCounterName,
            description: "MCP tool calls by kind, service and outcome.");

    private static readonly Histogram<double> ToolCallDurationHistogram =
        Meter.CreateHistogram<double>(
            McpTelemetry.ToolCallDurationHistogramName,
            unit: "s",
            description: "MCP tool call duration.");

    private static readonly Counter<long> RegistryReadsCounter =
        Meter.CreateCounter<long>(
            McpTelemetry.RegistryReadsCounterName,
            description: "Registry reads by result (hit: the caller's tools were already translated for this registry revision; fetched: translated now).");

    public IMcpToolCallObservation BeginToolCall(string toolName)
    {
        var requestId = correlation.Current.RequestId;
        var activity = ActivitySource.StartActivity(McpTelemetry.ToolCallActivityName, ActivityKind.Server);

        activity?.SetTag(McpTelemetry.TagTool, toolName);
        activity?.SetTag(KaleidoTelemetryTags.RequestId, requestId);

        logger.LogDebug("Kaleido MCP tool {Tool} called (request {RequestId}).", toolName, requestId);

        return new ToolCallObservation(activity, toolName, requestId, logger);
    }

    public IMcpToolsListObservation BeginToolsList()
    {
        var activity = ActivitySource.StartActivity(McpTelemetry.ToolsListActivityName, ActivityKind.Server);

        activity?.SetTag(KaleidoTelemetryTags.RequestId, correlation.Current.RequestId);

        return new ToolsListObservation(activity, logger);
    }

    public void RegistryRead(string result, string? revision)
    {
        RegistryReadsCounter.Add(1, new KeyValuePair<string, object?>(McpTelemetry.TagRegistryResult, result));

        Activity.Current?.SetTag(McpTelemetry.TagRegistryResult, result);
        Activity.Current?.SetTag(McpTelemetry.TagRegistryRevision, revision);

        logger.LogDebug("Kaleido MCP registry read: {Result} (revision {Revision}).", result, revision);
    }

    private sealed class ToolCallObservation(
        Activity? activity,
        string toolName,
        string requestId,
        ILogger logger)
        : IMcpToolCallObservation
    {
        private readonly long started = Stopwatch.GetTimestamp();
        private string kind = "unknown";
        private string service = string.Empty;
        private bool recorded;

        public void Resolved(KaleidoTool tool)
        {
            kind = tool.Kind switch
            {
                KaleidoToolKind.Query => "query",
                KaleidoToolKind.Step => "step",
                KaleidoToolKind.AnswerInformationRequest => "answer",
                KaleidoToolKind.GetProcessState => "state",
                _ => "unknown",
            };

            service = tool.Service;

            activity?.SetTag(McpTelemetry.TagToolKind, kind);
            activity?.SetTag(McpTelemetry.TagService, tool.Service);
            activity?.SetTag(McpTelemetry.TagCapability, tool.CapabilityName);
        }

        public void Completed(CallToolResult result)
        {
            var body = Body(result);
            var processId = body?["processId"]?.GetValue<string>();

            if (processId is not null)
            {
                activity?.SetTag(McpTelemetry.TagProcessId, processId);
            }

            if (result.IsError == true)
            {
                var code = body?["code"]?.GetValue<string>() ?? McpErrorCodes.InternalError;

                activity?.SetStatus(ActivityStatusCode.Error, code);
                activity?.SetTag(McpTelemetry.TagErrorCode, code);

                Record(McpTelemetry.OutcomeError, code);

                logger.LogInformation(
                    "Kaleido MCP tool {Tool} returned {ErrorCode} in {ElapsedMs} ms (request {RequestId}).",
                    toolName,
                    code,
                    ElapsedMilliseconds(),
                    requestId);

                return;
            }

            Record(McpTelemetry.OutcomeCompleted, errorCode: null);

            logger.LogInformation(
                "Kaleido MCP tool {Tool} completed in {ElapsedMs} ms (request {RequestId}, process {ProcessId}).",
                toolName,
                ElapsedMilliseconds(),
                requestId,
                processId);
        }

        public void InputRequired()
        {
            Record(McpTelemetry.OutcomeInputRequired, errorCode: null);

            logger.LogInformation(
                "Kaleido MCP tool {Tool} asked the user for input in {ElapsedMs} ms (request {RequestId}).",
                toolName,
                ElapsedMilliseconds(),
                requestId);
        }

        public void Canceled()
        {
            Record(McpTelemetry.OutcomeCanceled, errorCode: null);

            logger.LogWarning(
                "Kaleido MCP tool {Tool} was canceled by the caller after {ElapsedMs} ms (request {RequestId}).",
                toolName,
                ElapsedMilliseconds(),
                requestId);
        }

        public void Dispose() => activity?.Dispose();

        private void Record(string outcome, string? errorCode)
        {
            if (recorded)
            {
                return;
            }

            recorded = true;

            activity?.SetTag(McpTelemetry.TagOutcome, outcome);

            TagList tags =
            [
                new(McpTelemetry.TagToolKind, kind),
                new(McpTelemetry.TagService, service),
                new(McpTelemetry.TagOutcome, outcome),
            ];

            if (errorCode is not null)
            {
                tags.Add(McpTelemetry.TagErrorCode, errorCode);
            }

            ToolCallsCounter.Add(1, tags);
            ToolCallDurationHistogram.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        }

        private long ElapsedMilliseconds() =>
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        private static JsonObject? Body(CallToolResult result)
        {
            var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;

            if (string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{'))
            {
                return null;
            }

            try
            {
                return JsonNode.Parse(text) as JsonObject;
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
    }

    private sealed class ToolsListObservation(
        Activity? activity,
        ILogger logger)
        : IMcpToolsListObservation
    {
        public void Completed(ToolCatalog catalog)
        {
            activity?.SetTag(McpTelemetry.TagToolCount, catalog.Tools.Count);
            activity?.SetTag(McpTelemetry.TagRegistryPartial, catalog.IsPartial);
            activity?.SetTag(McpTelemetry.TagRegistryRevision, catalog.Revision);

            if (catalog.Unavailable is { } reason)
            {
                activity?.SetStatus(ActivityStatusCode.Error, McpErrorCodes.ServiceUnavailable);
                activity?.SetTag(McpTelemetry.TagErrorCode, McpErrorCodes.ServiceUnavailable);

                logger.LogDebug("Kaleido MCP listed no tools: {Reason}", reason);

                return;
            }

            logger.LogDebug("Kaleido MCP listed {Count} tool(s) (revision {Revision}).", catalog.Tools.Count, catalog.Revision);
        }

        public void Dispose() => activity?.Dispose();
    }
}
