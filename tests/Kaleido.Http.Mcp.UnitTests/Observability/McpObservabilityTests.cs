using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Kaleido.Http.Mcp.Tools;
using Kaleido.Observability;
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

    public McpObservabilityTests()
    {
        activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == McpTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
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

    private McpObservability CreateSut()
    {
        var correlation = new Mock<IKaleidoCorrelationContextAccessor>();
        correlation.Setup(x => x.Current).Returns(new KaleidoCorrelationContext { RequestId = requestId });

        return new McpObservability(correlation.Object, NullLogger<McpObservability>.Instance);
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
            call.Resolved(Step);
            call.Completed(Result("""{"processId":"p-1","outcome":"completed"}"""));
        }

        var span = Span(McpTelemetry.ToolCallActivityName);
        Assert.Equal("clinic_start_visit", span.GetTagItem(McpTelemetry.TagTool));
        Assert.Equal("step", span.GetTagItem(McpTelemetry.TagToolKind));
        Assert.Equal("clinic", span.GetTagItem(McpTelemetry.TagService));
        Assert.Equal("StartVisitStep", span.GetTagItem(McpTelemetry.TagCapability));
        Assert.Equal("p-1", span.GetTagItem(McpTelemetry.TagProcessId));
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
            call.Resolved(Step);
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

    public void Dispose()
    {
        activityListener.Dispose();
        meterListener.Dispose();
    }
}
