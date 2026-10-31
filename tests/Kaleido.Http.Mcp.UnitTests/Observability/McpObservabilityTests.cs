using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kaleido.Http.Mcp.Tools;
using Kaleido.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Observability.UnitTests;

public sealed class McpObservabilityTests
    : Kaleido.UnitTests.SutFixture, IDisposable
{
    // Each test uses its own request id, so spans and measurements from parallel tests never mix.
    private readonly string requestId = $"req-{Guid.NewGuid():N}";
    private readonly ConcurrentBag<Activity> activities = [];
    private readonly ConcurrentBag<(string Instrument, IReadOnlyDictionary<string, object?> Tags)> measurements = [];
    private readonly ActivityListener activityListener;
    private readonly MeterListener meterListener;
    private bool sampled = true;

    public McpObservabilityTests()
    {
        activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == McpTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => sampled ? ActivitySamplingResult.AllDataAndRecorded : ActivitySamplingResult.None,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(activityListener);

        meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == McpTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) => Record(instrument, tags));
        meterListener.SetMeasurementEventCallback<double>((instrument, _, tags, _) => Record(instrument, tags));
        meterListener.Start();
    }

    private void Record(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        measurements.Add((instrument.Name, tags.ToArray().ToDictionary(x => x.Key, x => x.Value)));

    private McpObservability CreateSut(ILogger<McpObservability>? logger = null)
    {
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { RequestId = requestId });

        return new McpObservability(correlation.Object, logger ?? NullLogger<McpObservability>.Instance);
    }

    private sealed class RecordingLogger : ILogger<McpObservability>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static readonly KaleidoTool Step = new()
    {
        Definition = new Tool { Name = "clinic_start_visit" },
        Kind = KaleidoToolKind.Step,
        Service = "clinic",
        CapabilityName = "StartVisitStep"
    };

    private Activity Span(string name) =>
        Assert.Single(activities, x => x.OperationName == name && Equals(x.GetTagItem(KaleidoTelemetryTags.RequestId), requestId));

    private static CallToolResult Result(string json, bool isError = false) =>
        new() { IsError = isError, Content = [new TextContentBlock { Text = json }] };

    [Fact]
    public void ACompletedToolCall_IsTracedAndCounted_WithItsToolAndProcess()
    {
        using (var call = CreateSut().BeginToolCall("clinic_start_visit"))
        {
            call.Resolved(Step, processId: null);
            call.Completed(Result("""{"processId":"p-1","outcome":"completed"}"""));
        }

        var span = Span(McpTelemetry.ToolCallActivityName);
        Assert.Equal("clinic_start_visit", span.GetTagItem(McpTelemetry.TagTool));
        Assert.Equal("step", span.GetTagItem(McpTelemetry.TagToolKind));
        Assert.Equal("clinic", span.GetTagItem(McpTelemetry.TagService));
        Assert.Equal("StartVisitStep", span.GetTagItem(McpTelemetry.TagCapability));
        Assert.Equal("p-1", span.GetTagItem(KaleidoTelemetryTags.ProcessId));
        Assert.Equal(McpTelemetry.OutcomeCompleted, span.GetTagItem(McpTelemetry.TagOutcome));
        Assert.NotEqual(ActivityStatusCode.Error, span.Status);

        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.ToolCallsCounterName &&
            Equals(x.Tags[McpTelemetry.TagOutcome], McpTelemetry.OutcomeCompleted) &&
            Equals(x.Tags[McpTelemetry.TagService], "clinic"));
        Assert.Contains(measurements, x => x.Instrument == McpTelemetry.ToolCallDurationHistogramName);
    }

    [Fact]
    public void AnErrorResult_MarksTheSpan_AndCountsTheErrorCode()
    {
        using (var call = CreateSut().BeginToolCall("clinic_close_visit"))
        {
            call.Resolved(Step, processId: null);
            call.Completed(Result("""{"code":"mcp_invalid_arguments","message":"x"}""", isError: true));
        }

        var span = Span(McpTelemetry.ToolCallActivityName);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(McpErrorCodes.InvalidArguments, span.GetTagItem(McpTelemetry.TagErrorCode));
        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.ToolCallsCounterName &&
            Equals(x.Tags[McpTelemetry.TagOutcome], McpTelemetry.OutcomeError) &&
            Equals(x.Tags[McpTelemetry.TagErrorCode], McpErrorCodes.InvalidArguments));
    }

    [Fact]
    public void TheProcessIdArgument_TagsTheSpan_EvenWhenTheResultHasNone()
    {
        using (var call = CreateSut().BeginToolCall("clinic_patient"))
        {
            call.Resolved(Step, "p-7");
            call.Completed(Result("""{"code":"mcp_service_error","message":"x"}""", isError: true));
        }

        Assert.Equal("p-7", Span(McpTelemetry.ToolCallActivityName).GetTagItem(KaleidoTelemetryTags.ProcessId));
    }

    [Fact]
    public void AnErrorWithoutACode_IsCountedAsAnInternalError()
    {
        using (var call = CreateSut().BeginToolCall("x"))
        {
            call.Completed(Result("not json", isError: true));
        }

        Assert.Equal(McpErrorCodes.InternalError, Span(McpTelemetry.ToolCallActivityName).GetTagItem(McpTelemetry.TagErrorCode));
    }

    [Fact]
    public void InputRequired_AndCanceled_AreOutcomes_NotFailures()
    {
        using (var call = CreateSut().BeginToolCall("clinic_start_visit"))
        {
            call.InputRequired();
            call.Canceled();
        }

        var span = Span(McpTelemetry.ToolCallActivityName);
        Assert.Equal(McpTelemetry.OutcomeInputRequired, span.GetTagItem(McpTelemetry.TagOutcome));
        Assert.NotEqual(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public void AToolCall_IsRecordedOnce_EvenIfReportedTwice()
    {
        using (var call = CreateSut().BeginToolCall("once"))
        {
            call.Completed(Result("{}"));
            call.Canceled();
        }

        Assert.Equal(McpTelemetry.OutcomeCompleted, Span(McpTelemetry.ToolCallActivityName).GetTagItem(McpTelemetry.TagOutcome));
    }

    [Fact]
    public void AToolsListing_IsTraced_WithTheCatalogsSize()
    {
        using (var listing = CreateSut().BeginToolsList())
        {
            listing.Completed(new ToolCatalog { Revision = "r3", Tools = [Step] });
        }

        var span = Span(McpTelemetry.ToolsListActivityName);
        Assert.Equal(1, span.GetTagItem(McpTelemetry.TagToolCount));
        Assert.Equal("r3", span.GetTagItem(McpTelemetry.TagRegistryRevision));
    }

    [Fact]
    public void AnUnavailableListing_MarksTheSpan()
    {
        using (var listing = CreateSut().BeginToolsList())
        {
            listing.Completed(new ToolCatalog { Unavailable = "down" });
        }

        var span = Span(McpTelemetry.ToolsListActivityName);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(McpErrorCodes.ServiceUnavailable, span.GetTagItem(McpTelemetry.TagErrorCode));
    }

    [Fact]
    public void RegistryRead_IsCountedByResult()
    {
        var revision = $"rev-{Guid.NewGuid():N}";

        using (var listing = CreateSut().BeginToolsList())
        {
            CreateSut().RegistryRead(McpTelemetry.RegistryHit, revision);
            listing.Completed(ToolCatalog.Empty);
        }

        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.RegistryReadsCounterName &&
            Equals(x.Tags[McpTelemetry.TagRegistryResult], McpTelemetry.RegistryHit));
        Assert.Equal(McpTelemetry.RegistryHit, Span(McpTelemetry.ToolsListActivityName).GetTagItem(McpTelemetry.TagRegistryResult));
    }

    // #452: a lookup state signed with a key this instance lacks is a deployment problem: a Warning that
    // names the key and the fix, and a count by reason.
    [Fact]
    public void AStateRefusedForAnUnknownKey_WarnsWithTheFix_AndIsCounted()
    {
        var logger = new RecordingLogger();
        var keyId = Guid.NewGuid();

        CreateSut(logger).StateRefused(McpTelemetry.StateKindLookup, McpTelemetry.StateUnknownKey, keyId);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains(keyId.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains("do not share one key ring", entry.Message, StringComparison.Ordinal);
        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.StatesRefusedCounterName &&
            Equals(x.Tags[McpTelemetry.TagStateRefusal], McpTelemetry.StateUnknownKey) &&
            Equals(x.Tags[McpTelemetry.TagStateKind], McpTelemetry.StateKindLookup));
    }

    // A refusal inside a tool call tags the call's span with the reason.
    [Fact]
    public void AStateRefusedDuringAToolCall_TagsTheSpanWithTheReason()
    {
        using (var call = CreateSut().BeginToolCall("clinic_assign_bed"))
        {
            CreateSut().StateRefused(McpTelemetry.StateKindAnswers, McpTelemetry.StateExpired, keyId: null);
            call.Completed(Result("""{"code":"mcp_invalid_arguments"}""", isError: true));
        }

        Assert.Equal(McpTelemetry.StateExpired, Span(McpTelemetry.ToolCallActivityName).GetTagItem(McpTelemetry.TagStateRefusal));
        Assert.Equal(McpTelemetry.StateKindAnswers, Span(McpTelemetry.ToolCallActivityName).GetTagItem(McpTelemetry.TagStateKind));
    }

    // Outside any span (no Activity.Current) the refusal is still counted and logged.
    [Fact]
    public void AStateRefusedOutsideASpan_IsStillCountedAndLogged()
    {
        var logger = new RecordingLogger();
        Activity.Current = null;

        CreateSut(logger).StateRefused(McpTelemetry.StateKindContinuation, McpTelemetry.StateInvalid, keyId: null);

        Assert.Single(logger.Entries);
        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.StatesRefusedCounterName &&
            Equals(x.Tags[McpTelemetry.TagStateRefusal], McpTelemetry.StateInvalid));
    }

    // The other reasons are the client's (a changed, expired or foreign state): Debug, and counted.
    [Theory]
    [InlineData(McpTelemetry.StateExpired)]
    [InlineData(McpTelemetry.StateAnotherCaller)]
    [InlineData(McpTelemetry.StateInvalid)]
    public void AStateRefusedForAnotherReason_IsLoggedAtDebug_AndCounted(string reason)
    {
        var logger = new RecordingLogger();

        CreateSut(logger).StateRefused(McpTelemetry.StateKindConfirmation, reason, keyId: null);

        Assert.Equal((LogLevel.Debug, $"Kaleido MCP: a confirmation state was refused ({reason}); nothing was run."), Assert.Single(logger.Entries));
        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.StatesRefusedCounterName &&
            Equals(x.Tags[McpTelemetry.TagStateRefusal], reason));
    }

    [Theory]
    [InlineData(KaleidoToolKind.Query, "query")]
    [InlineData(KaleidoToolKind.Step, "step")]
    [InlineData(KaleidoToolKind.AnswerInformationRequest, "answer")]
    [InlineData(KaleidoToolKind.GetProcessState, "state")]
    [InlineData(KaleidoToolKind.SearchTools, "unknown")]
    [InlineData(KaleidoToolKind.DescribeTool, "unknown")]
    public void AResolvedTool_TagsItsKind_OnTheSpanAndTheMetrics(object kind, string expected)
    {
        using (var call = CreateSut().BeginToolCall("x"))
        {
            call.Resolved(Step with { Kind = (KaleidoToolKind)kind }, processId: null);
            call.Completed(Result("{}"));
        }

        Assert.Equal(expected, Span(McpTelemetry.ToolCallActivityName).GetTagItem(McpTelemetry.TagToolKind));
        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.ToolCallsCounterName &&
            Equals(x.Tags[McpTelemetry.TagToolKind], expected) &&
            !x.Tags.ContainsKey(McpTelemetry.TagErrorCode));
    }

    [Fact]
    public void AnUnresolvedToolCall_IsCountedAsAnUnknownKind_OfNoService()
    {
        using (var call = CreateSut().BeginToolCall("clinic_chart"))
        {
            call.Completed(Result("""{"code":"mcp_unknown_tool","message":"x"}""", isError: true));
        }

        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.ToolCallsCounterName &&
            Equals(x.Tags[McpTelemetry.TagToolKind], "unknown") &&
            Equals(x.Tags[McpTelemetry.TagService], string.Empty) &&
            Equals(x.Tags[McpTelemetry.TagErrorCode], McpErrorCodes.UnknownTool));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankProcessIdArgument_DoesNotTagTheSpan(string processId)
    {
        using (var call = CreateSut().BeginToolCall("clinic_start_visit"))
        {
            call.Resolved(Step, processId);
            call.Completed(Result("""{"outcome":"completed"}"""));
        }

        Assert.Null(Span(McpTelemetry.ToolCallActivityName).GetTagItem(KaleidoTelemetryTags.ProcessId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("[1,2]")]
    [InlineData("{not json")]
    [InlineData("""{"message":"no code"}""")]
    public void AnErrorWhoseBodyHasNoCode_IsAnInternalError(string? text)
    {
        var result = text is null
            ? new CallToolResult { IsError = true, Content = [] }
            : Result(text, isError: true);

        using (var call = CreateSut().BeginToolCall("x"))
        {
            call.Completed(result);
        }

        var span = Span(McpTelemetry.ToolCallActivityName);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(McpErrorCodes.InternalError, span.StatusDescription);
        Assert.Equal(McpErrorCodes.InternalError, span.GetTagItem(McpTelemetry.TagErrorCode));
        Assert.Null(span.GetTagItem(KaleidoTelemetryTags.ProcessId));
    }

    [Fact]
    public void ACanceledToolCall_IsCountedAsCanceled_NotAsAnError()
    {
        using (var call = CreateSut().BeginToolCall("clinic_start_visit"))
        {
            call.Resolved(Step, processId: null);
            call.Canceled();
        }

        var span = Span(McpTelemetry.ToolCallActivityName);
        Assert.Equal(McpTelemetry.OutcomeCanceled, span.GetTagItem(McpTelemetry.TagOutcome));
        Assert.NotEqual(ActivityStatusCode.Error, span.Status);
        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.ToolCallDurationHistogramName &&
            Equals(x.Tags[McpTelemetry.TagOutcome], McpTelemetry.OutcomeCanceled) &&
            Equals(x.Tags[McpTelemetry.TagService], "clinic"));
    }

    [Fact]
    public void AToolCall_LogsItsStartAtDebug_AndItsCompletionAtInformation()
    {
        var logger = new RecordingLogger();

        for (var i = 0; i < 2; i++)
        {
            using var call = CreateSut(logger).BeginToolCall("clinic_start_visit");
            call.Completed(Result("""{"processId":"p-1"}"""));
        }

        Assert.Equal(4, logger.Entries.Count);
        Assert.Equal((LogLevel.Debug, $"Kaleido MCP tool clinic_start_visit called (request {requestId})."), logger.Entries[0]);
        Assert.Equal(LogLevel.Information, logger.Entries[1].Level);
        Assert.Matches($@"^Kaleido MCP tool clinic_start_visit completed in \d+ ms \(request {requestId}, process p-1\)\.$", logger.Entries[1].Message);
        Assert.Equal(logger.Entries[0], logger.Entries[2]);
    }

    [Fact]
    public void ACompletionWithoutAProcess_LogsTheProcessAsNull()
    {
        var logger = new RecordingLogger();

        using (var call = CreateSut(logger).BeginToolCall("clinic_patient"))
        {
            call.Completed(Result("""{"results":[]}"""));
        }

        Assert.Matches($@"^Kaleido MCP tool clinic_patient completed in \d+ ms \(request {requestId}, process \(null\)\)\.$", logger.Entries[^1].Message);
    }

    [Fact]
    public void EachOutcome_IsLoggedOnce_AtItsLevel()
    {
        var logger = new RecordingLogger();

        for (var i = 0; i < 2; i++)
        {
            using (var failed = CreateSut(logger).BeginToolCall("clinic_close_visit"))
            {
                failed.Completed(Result("""{"code":"mcp_service_error","message":"x"}""", isError: true));
            }

            using (var asking = CreateSut(logger).BeginToolCall("clinic_start_visit"))
            {
                asking.InputRequired();
            }

            using (var canceled = CreateSut(logger).BeginToolCall("clinic_patient"))
            {
                canceled.Canceled();
            }
        }

        var outcomes = logger.Entries.Where(x => x.Level != LogLevel.Debug).ToArray();
        Assert.Equal(6, outcomes.Length);
        Assert.Equal(LogLevel.Information, outcomes[0].Level);
        Assert.Matches($@"^Kaleido MCP tool clinic_close_visit returned mcp_service_error in \d+ ms \(request {requestId}\)\.$", outcomes[0].Message);
        Assert.Equal(LogLevel.Information, outcomes[1].Level);
        Assert.Matches($@"^Kaleido MCP tool clinic_start_visit asked the user for input in \d+ ms \(request {requestId}\)\.$", outcomes[1].Message);
        Assert.Equal(LogLevel.Warning, outcomes[2].Level);
        Assert.Matches($@"^Kaleido MCP tool clinic_patient was canceled by the caller after \d+ ms \(request {requestId}\)\.$", outcomes[2].Message);
        Assert.Equal(outcomes[..3].Select(x => x.Level), outcomes[3..].Select(x => x.Level));
    }

    [Fact]
    public void AToolsListing_LogsWhatItListed_AtDebug()
    {
        var logger = new RecordingLogger();

        for (var i = 0; i < 2; i++)
        {
            using (var listing = CreateSut(logger).BeginToolsList())
            {
                listing.Completed(new ToolCatalog { Revision = "r3", Tools = [Step] });
            }

            using (var unrevised = CreateSut(logger).BeginToolsList())
            {
                unrevised.Completed(ToolCatalog.Empty);
            }

            using (var unavailable = CreateSut(logger).BeginToolsList())
            {
                unavailable.Completed(new ToolCatalog { Unavailable = "registry down" });
            }
        }

        Assert.All(logger.Entries, x => Assert.Equal(LogLevel.Debug, x.Level));
        Assert.Equal(
            [
                "Kaleido MCP listed 1 tool(s) (revision r3).",
                "Kaleido MCP listed 0 tool(s) (revision (null)).",
                "Kaleido MCP listed no tools: registry down",
                "Kaleido MCP listed 1 tool(s) (revision r3).",
                "Kaleido MCP listed 0 tool(s) (revision (null)).",
                "Kaleido MCP listed no tools: registry down"
            ],
            logger.Entries.Select(x => x.Message));
    }

    [Fact]
    public void APartialListing_IsTaggedAsPartial()
    {
        using (var listing = CreateSut().BeginToolsList())
        {
            listing.Completed(new ToolCatalog { Revision = "r4", IsPartial = true });
        }

        var span = Span(McpTelemetry.ToolsListActivityName);
        Assert.Equal(true, span.GetTagItem(McpTelemetry.TagRegistryPartial));
        Assert.Equal(0, span.GetTagItem(McpTelemetry.TagToolCount));
        Assert.NotEqual(ActivityStatusCode.Error, span.Status);
    }

    [Fact]
    public void RegistryRead_OutsideASpan_IsStillCountedAndLogged()
    {
        var logger = new RecordingLogger();
        var revision = $"rev-{Guid.NewGuid():N}";
        Assert.Null(Activity.Current);

        CreateSut(logger).RegistryRead(McpTelemetry.RegistryFetched, revision);
        CreateSut(logger).RegistryRead(McpTelemetry.RegistryFetched, revision: null);

        Assert.Equal(2, measurements.Count(x =>
            x.Instrument == McpTelemetry.RegistryReadsCounterName &&
            Equals(x.Tags[McpTelemetry.TagRegistryResult], McpTelemetry.RegistryFetched)));
        Assert.Equal(
            [
                (LogLevel.Debug, $"Kaleido MCP registry read: fetched (revision {revision})."),
                (LogLevel.Debug, "Kaleido MCP registry read: fetched (revision (null)).")
            ],
            logger.Entries);
    }

    [Fact]
    public void WhenNobodyRecordsSpans_CallsAndListingsAreStillCountedAndLogged()
    {
        sampled = false;
        var logger = new RecordingLogger();

        using (var call = CreateSut(logger).BeginToolCall("clinic_start_visit"))
        {
            call.Resolved(Step, "p-9");
            call.Completed(Result("""{"processId":"p-9","code":"mcp_service_error"}""", isError: true));
            call.Canceled();
        }

        using (var asking = CreateSut(logger).BeginToolCall("clinic_start_visit"))
        {
            asking.InputRequired();
        }

        using (var listing = CreateSut(logger).BeginToolsList())
        {
            listing.Completed(new ToolCatalog { Unavailable = "down" });
        }

        Assert.Empty(activities);
        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.ToolCallsCounterName &&
            Equals(x.Tags[McpTelemetry.TagOutcome], McpTelemetry.OutcomeError) &&
            Equals(x.Tags[McpTelemetry.TagErrorCode], McpErrorCodes.ServiceError));
        Assert.Contains(measurements, x =>
            x.Instrument == McpTelemetry.ToolCallsCounterName &&
            Equals(x.Tags[McpTelemetry.TagOutcome], McpTelemetry.OutcomeInputRequired));
        Assert.Contains(logger.Entries, x => x.Message == "Kaleido MCP listed no tools: down");
        Assert.Contains(logger.Entries, x => x.Message.Contains("returned mcp_service_error", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        activityListener.Dispose();
        meterListener.Dispose();
    }
}
