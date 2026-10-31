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

    /// <summary>
    /// A state handed to the client was refused when it came back (#452, #516), with its kind
    /// (<see cref="McpTelemetry.StateKindLookup"/> and the others) and the reason
    /// (<see cref="McpTelemetry.StateUnknownKey"/> and the others). An unknown key is a deployment
    /// problem - the instances do not share a Data Protection key ring - and is logged at Warning with
    /// the fix; the other reasons are the client's, at Debug.
    /// </summary>
    void StateRefused(string kind, string reason, Guid? keyId);
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

    private static readonly Counter<long> StatesRefusedCounter =
        Meter.CreateCounter<long>(
            McpTelemetry.StatesRefusedCounterName,
            description: "States handed to the client and refused when they came back, by kind and reason (unknown_key: the instances do not share a Data Protection key ring).");

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

    public void StateRefused(string kind, string reason, Guid? keyId)
    {
        StatesRefusedCounter.Add(
            1,
            new KeyValuePair<string, object?>(McpTelemetry.TagStateKind, kind),
            new KeyValuePair<string, object?>(McpTelemetry.TagStateRefusal, reason));

        Activity.Current?.SetTag(McpTelemetry.TagStateKind, kind);
        Activity.Current?.SetTag(McpTelemetry.TagStateRefusal, reason);

        if (string.Equals(reason, McpTelemetry.StateUnknownKey, StringComparison.Ordinal))
        {
            LogStateKeyNotInRing(logger, kind, keyId);
        }
        else
        {
            LogStateRefused(logger, kind, reason);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido MCP: a form was answered with {Kind} state protected by Data Protection key {KeyId}, which this instance's key ring does not have, so it was refused and the user must start again. The MCP instances behind the load balancer do not share one key ring: configure AddDataProtection().SetApplicationName(...).PersistKeysTo...() the same on every instance (docs/guide/deployment.md).")]
    private static partial void LogStateKeyNotInRing(ILogger logger, string kind, Guid? keyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Kaleido MCP: a {Kind} state was refused ({Reason}); nothing was run.")]
    private static partial void LogStateRefused(ILogger logger, string kind, string reason);

    private sealed partial class ToolCallObservation(
        Activity? activity,
        string toolName,
        string requestId,
        ILogger logger)
        : IMcpToolCallObservation
    {
        private readonly long _started = Stopwatch.GetTimestamp();
        private string _kind = "unknown";
        private string _service = string.Empty;
        private bool _recorded;

        public void Resolved(KaleidoTool tool, string? processId)
        {
            if (!string.IsNullOrWhiteSpace(processId))
            {
                activity?.SetTag(KaleidoTelemetryTags.ProcessId, processId);
            }

            _kind = tool.Kind switch
            {
                KaleidoToolKind.Query => "query",
                KaleidoToolKind.Step => "step",
                KaleidoToolKind.AnswerInformationRequest => "answer",
                KaleidoToolKind.GetProcessState => "state",
                _ => "unknown",
            };

            _service = tool.Service;

            activity?.SetTag(McpTelemetry.TagToolKind, _kind);
            activity?.SetTag(McpTelemetry.TagService, tool.Service);
            activity?.SetTag(McpTelemetry.TagCapability, tool.CapabilityName);
        }

        public void Completed(CallToolResult result)
        {
            var body = Body(result);
            var processId = body?["processId"]?.GetValue<string>();

            if (processId is not null)
            {
                activity?.SetTag(KaleidoTelemetryTags.ProcessId, processId);
            }

            if (result.IsError == true)
            {
                var (code, refused) = Classify(body);

                activity?.SetTag(McpTelemetry.TagErrorCode, code);

                if (refused)
                {
                    // The caller's request, refused: answered to the caller, not a failure of the adapter
                    // (as the runtimes mark a refused request), so the span stays Unset.
                    LogToolRefused(logger, toolName, code, Record(McpTelemetry.OutcomeRefused, code), requestId);

                    return;
                }

                activity?.SetStatus(ActivityStatusCode.Error, code);

                LogToolReturnedError(logger, toolName, code, Record(McpTelemetry.OutcomeError, code), requestId);

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
            var elapsed = Stopwatch.GetElapsedTime(_started);

            if (_recorded)
            {
                return (long)elapsed.TotalMilliseconds;
            }

            _recorded = true;

            activity?.SetTag(McpTelemetry.TagOutcome, outcome);

            TagList tags =
            [
                new(McpTelemetry.TagToolKind, _kind),
                new(McpTelemetry.TagService, _service),
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

        // #525: the code tag is one of the adapter's tool-call codes - never a downstream service's own
        // code from the body, which would make the metric dimension open-ended - and the outcome tells a
        // refused request (the caller's to fix) from a failure. A service that answered 4xx refused the
        // call; a 5xx, or no answer, is a failure.
        private static (string Code, bool Refused) Classify(JsonObject? body)
        {
            var raw = body?["code"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
            var code = raw is not null && ToolCallCodes.Contains(raw) ? raw : McpErrorCodes.InternalError;
            var status = body?["details"]?["status"] is JsonValue given && given.TryGetValue<int>(out var number) ? number : (int?)null;

            var refused = RefusedCodes.Contains(code) ||
                          (string.Equals(code, McpErrorCodes.ServiceError, StringComparison.Ordinal) && status is >= 400 and < 500);

            return (code, refused);
        }

        // The codes a tool call can answer with (the others are startup codes).
        private static readonly HashSet<string> ToolCallCodes = new(StringComparer.Ordinal)
        {
            McpErrorCodes.UnknownTool,
            McpErrorCodes.InvalidArguments,
            McpErrorCodes.ServiceError,
            McpErrorCodes.ProcessConflict,
            McpErrorCodes.ServiceUnavailable,
            McpErrorCodes.InternalError,
            McpErrorCodes.NoPendingInformationRequest,
            McpErrorCodes.ConfirmationRequired,
        };

        // The caller's mistakes, or a request the process refuses as it stands.
        private static readonly HashSet<string> RefusedCodes = new(StringComparer.Ordinal)
        {
            McpErrorCodes.UnknownTool,
            McpErrorCodes.InvalidArguments,
            McpErrorCodes.ProcessConflict,
            McpErrorCodes.NoPendingInformationRequest,
            McpErrorCodes.ConfirmationRequired,
        };

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

        [LoggerMessage(Level = LogLevel.Information, Message = "Kaleido MCP tool {Tool} refused the call with {ErrorCode} in {ElapsedMs} ms (request {RequestId}).")]
        private static partial void LogToolRefused(ILogger logger, string tool, string errorCode, long elapsedMs, string requestId);

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
