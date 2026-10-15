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
    /// <summary>
    /// The tool was found: tag its kind, service and capability, and the process the call names in
    /// its <c>processId</c> argument (queries included, which return no process of their own).
    /// </summary>
    void Resolved(KaleidoTool tool, string? processId);

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

internal sealed partial class McpObservability(
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

        LogToolCalled(logger, toolName, requestId);

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

        LogRegistryRead(logger, result, revision);
    }

    private sealed partial class ToolCallObservation(
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

        public void Resolved(KaleidoTool tool, string? processId)
        {
            if (!string.IsNullOrWhiteSpace(processId))
            {
                activity?.SetTag(McpTelemetry.TagProcessId, processId);
            }

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

                var elapsedMs = Record(McpTelemetry.OutcomeError, code);

                LogToolReturnedError(logger, toolName, code, elapsedMs, requestId);

                return;
            }

            var completedMs = Record(McpTelemetry.OutcomeCompleted, errorCode: null);

            LogToolCompleted(logger, toolName, completedMs, requestId, processId);
        }

        public void InputRequired()
        {
            var elapsedMs = Record(McpTelemetry.OutcomeInputRequired, errorCode: null);

            LogToolAskedForInput(logger, toolName, elapsedMs, requestId);
        }

        public void Canceled()
        {
            var elapsedMs = Record(McpTelemetry.OutcomeCanceled, errorCode: null);

            LogToolCanceled(logger, toolName, elapsedMs, requestId);
        }

        public void Dispose() => activity?.Dispose();

        // Records the outcome metrics once and returns the call's duration in milliseconds, so the
        // log reports the same measurement as the duration histogram.
        private long Record(string outcome, string? errorCode)
        {
            var elapsed = Stopwatch.GetElapsedTime(started);

            if (recorded)
            {
                return (long)elapsed.TotalMilliseconds;
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
            ToolCallDurationHistogram.Record(elapsed.TotalSeconds, tags);

            return (long)elapsed.TotalMilliseconds;
        }

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

        [LoggerMessage(Level = LogLevel.Information, Message = "Kaleido MCP tool {Tool} returned {ErrorCode} in {ElapsedMs} ms (request {RequestId}).")]
        private static partial void LogToolReturnedError(ILogger logger, string tool, string errorCode, long elapsedMs, string requestId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Kaleido MCP tool {Tool} completed in {ElapsedMs} ms (request {RequestId}, process {ProcessId}).")]
        private static partial void LogToolCompleted(ILogger logger, string tool, long elapsedMs, string requestId, string? processId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Kaleido MCP tool {Tool} asked the user for input in {ElapsedMs} ms (request {RequestId}).")]
        private static partial void LogToolAskedForInput(ILogger logger, string tool, long elapsedMs, string requestId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP tool {Tool} was canceled by the caller after {ElapsedMs} ms (request {RequestId}).")]
        private static partial void LogToolCanceled(ILogger logger, string tool, long elapsedMs, string requestId);
    }

    private sealed partial class ToolsListObservation(
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

                LogNoTools(logger, reason);

                return;
            }

            LogToolsListed(logger, catalog.Tools.Count, catalog.Revision);
        }

        public void Dispose() => activity?.Dispose();

        [LoggerMessage(Level = LogLevel.Debug, Message = "Kaleido MCP listed no tools: {Reason}")]
        private static partial void LogNoTools(ILogger logger, string reason);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Kaleido MCP listed {Count} tool(s) (revision {Revision}).")]
        private static partial void LogToolsListed(ILogger logger, int count, string? revision);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Kaleido MCP tool {Tool} called (request {RequestId}).")]
    private static partial void LogToolCalled(ILogger logger, string tool, string requestId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Kaleido MCP registry read: {Result} (revision {Revision}).")]
    private static partial void LogRegistryRead(ILogger logger, string result, string? revision);
}
