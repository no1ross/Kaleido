using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// Every tool call is correlated and traced: one request id (the client's, or a generated one) in
/// the result and on the tool-call span, which carries the tool, its kind, service and outcome.
/// </summary>
public sealed class McpObservabilityTests(McpClinicFixture fixture)
    : IClassFixture<McpClinicFixture>, IDisposable
{
    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private readonly ConcurrentBag<Activity> activities = [];
    private ActivityListener? listener;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheClientsRequestId_IsReturnedInTheResult()
    {
        var requestId = $"mcp-test-{Guid.NewGuid():N}";

        await using var client = await fixture.ConnectAsync(Nurse, requestId: requestId);

        var body = Body(await client.CallToolAsync(
            "clinic_patient",
            new Dictionary<string, object?> { ["searchText"] = "Ben" },
            cancellationToken: Cancel));

        Assert.Equal(requestId, body["requestId"]?.GetValue<string>());
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
        var requestId = $"mcp-span-{Guid.NewGuid():N}";

        await using var client = await fixture.ConnectAsync(Nurse, requestId: requestId);

        await client.CallToolAsync(
            "clinic_close_visit",
            new Dictionary<string, object?> { ["note"] = "no process" },
            cancellationToken: Cancel);

        var span = Assert.Single(
            activities,
            x => x.OperationName == McpTelemetry.ToolCallActivityName &&
                 Equals(x.GetTagItem(KaleidoTelemetryTags.RequestId), requestId));

        Assert.Equal("clinic_close_visit", span.GetTagItem(McpTelemetry.TagTool));
        Assert.Equal("step", span.GetTagItem(McpTelemetry.TagToolKind));
        Assert.Equal("clinic", span.GetTagItem(McpTelemetry.TagService));
        Assert.Equal(McpTelemetry.OutcomeError, span.GetTagItem(McpTelemetry.TagOutcome));
        Assert.Equal(McpErrorCodes.InvalidArguments, span.GetTagItem(McpTelemetry.TagErrorCode));
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
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().Single().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");
}
