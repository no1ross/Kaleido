using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// Every tool call is correlated and traced with the request id Kaleido's middleware resolved for
/// the MCP request (an authenticated caller's own X-Kaleido-Request-Id is kept; otherwise one is
/// generated) — in the result and on the tool-call span, which carries the tool, its kind, service
/// and outcome.
/// </summary>
public sealed class McpObservabilityTests(McpClinicFixture fixture)
    : IClassFixture<McpClinicFixture>, IDisposable
{
    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private readonly ConcurrentBag<Activity> activities = [];
    private ActivityListener? listener;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheResult_ReturnsTheAuthenticatedCallersRequestId_TheSameOneOnTheSpan()
    {
        Listen();
        var supplied = $"mcp-test-{Guid.NewGuid():N}";

        await using var client = await fixture.ConnectAsync(Nurse, requestId: supplied);

        var body = Body(await client.CallToolAsync(
            "clinic_patient",
            new Dictionary<string, object?> { ["searchText"] = "Ben" },
            cancellationToken: Cancel));

        var requestId = body["requestId"]?.GetValue<string>();
        Assert.Equal(supplied, requestId);
        Assert.Contains(
            activities,
            x => x.OperationName == McpTelemetry.ToolCallActivityName &&
                 Equals(x.GetTagItem(McpTelemetry.TagTool), "clinic_patient") &&
                 Equals(x.GetTagItem(KaleidoTelemetryTags.RequestId), requestId));
    }

    [Fact]
    public async Task WithoutARequestId_OneIsGenerated_AndReturned()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var body = Body(await client.CallToolAsync(
            "clinic_patient",
            new Dictionary<string, object?>(),
            cancellationToken: Cancel));

        Assert.False(string.IsNullOrWhiteSpace(body["requestId"]?.GetValue<string>()));
    }

    [Fact]
    public async Task AToolCall_EmitsASpan_WithTheToolAndItsOutcome()
    {
        Listen();

        await using var client = await fixture.ConnectAsync(Nurse);

        var requestId = Body(await client.CallToolAsync(
            "clinic_close_visit",
            new Dictionary<string, object?> { ["note"] = "no process" },
            cancellationToken: Cancel))["requestId"]?.GetValue<string>();

        var span = Assert.Single(
            activities,
            x => x.OperationName == McpTelemetry.ToolCallActivityName &&
                 Equals(x.GetTagItem(KaleidoTelemetryTags.RequestId), requestId));

        Assert.Equal("clinic_close_visit", span.GetTagItem(McpTelemetry.TagTool));
        Assert.Equal("step", span.GetTagItem(McpTelemetry.TagToolKind));
        Assert.Equal("clinic", span.GetTagItem(McpTelemetry.TagService));
        // #525: a call missing its processId is the caller's mistake - refused, not an error span.
        Assert.Equal(McpTelemetry.OutcomeRefused, span.GetTagItem(McpTelemetry.TagOutcome));
        Assert.Equal(McpErrorCodes.InvalidArguments, span.GetTagItem(McpTelemetry.TagErrorCode));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    public void Dispose() => listener?.Dispose();

    private void Listen()
    {
        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == McpTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };

        ActivitySource.AddActivityListener(listener);
    }

    private static JsonNode Body(CallToolResult result) =>
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().First().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");
}
