using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

// #312: the MCP trust boundary, proven against a real host - enforcement is the server's, never the
// prompt's; a user's cancellation and a slow service end cleanly; credentials and arguments never
// reach the logs or spans.
public sealed class McpTrustTests(McpClinicFixture fixture)
    : IClassFixture<McpClinicFixture>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static string TextOf(CallToolResult result) =>
        string.Concat(result.Content.OfType<TextContentBlock>().Select(x => x.Text));

    // Calling a tool by name is no way around the per-caller list: the doctor-only chart tool is
    // unknown to a nurse, and the service is never called.
    [Fact]
    public async Task AToolAnotherCallerHas_IsRefused_WithoutCallingTheService()
    {
        var nurse = BearerTestAuthHandler.Token($"nurse-{Guid.NewGuid():N}");
        await using var client = await fixture.ConnectAsync(nurse);

        var result = await client.CallToolAsync("clinic_chart", new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.True(result.IsError);
        Assert.Contains(McpErrorCodes.UnknownTool, TextOf(result), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ServiceRequests, x => x.Authorization == nurse && x.Path.Contains("/queryable/", StringComparison.Ordinal));
    }

    // The user stops waiting: the service call is aborted, and the adapter records one cancellation
    // (a Warning, the repository's rule for cancellations), never a failure (nothing at Error).
    [Fact]
    public async Task WhenTheUserCancelsMidCall_TheServiceCallIsAborted_AndItIsACancellationNotAFailure()
    {
        var requestId = $"cancel-{Guid.NewGuid():N}";
        fixture.SlowRequests[requestId] = TimeSpan.FromSeconds(30);
        await using var client = await fixture.ConnectAsync(BearerTestAuthHandler.Token("nina"), requestId: requestId);
        using var userGivesUp = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        userGivesUp.CancelAfter(TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.CallToolAsync("clinic_patient", new Dictionary<string, object?>(), cancellationToken: userGivesUp.Token).AsTask());

        await WaitUntilAsync(() => fixture.AbortedServiceRequests.Contains(requestId));
        await WaitUntilAsync(() => fixture.Logs.Any(x => x.Level == LogLevel.Warning && x.Text.Contains(requestId, StringComparison.Ordinal)));
        var logged = fixture.Logs.Where(x => x.Level >= LogLevel.Warning && x.Text.Contains(requestId, StringComparison.Ordinal)).ToArray();
        var warning = Assert.Single(logged);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("cancel", warning.Text, StringComparison.OrdinalIgnoreCase);
    }

    // A service slower than the client timeout: the agent gets a tool error with the request id
    // (to quote), within the timeout, instead of a hanging call.
    [Fact]
    public async Task ASlowService_EndsAsAToolError_WithTheRequestId_WithinTheClientTimeout()
    {
        var requestId = $"slow-{Guid.NewGuid():N}";
        fixture.SlowRequests[requestId] = TimeSpan.FromSeconds(30);
        await using var client = await fixture.ConnectAsync(BearerTestAuthHandler.Token("nina"), requestId: requestId);
        var started = System.Diagnostics.Stopwatch.StartNew();

        var result = await client.CallToolAsync("clinic_patient", new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.True(result.IsError);
        Assert.Contains(requestId, TextOf(result), StringComparison.Ordinal);
        Assert.InRange(started.Elapsed, McpClinicFixture.ClientTimeout - TimeSpan.FromSeconds(1), McpClinicFixture.ClientTimeout + TimeSpan.FromSeconds(10));
    }

    // The user's token and what the agent passes as arguments are the user's data: neither is written
    // to the adapter's or the service's logs, nor to span tags.
    [Fact]
    public async Task TheUsersTokenAndToolArguments_NeverReachTheLogsOrSpans()
    {
        var secretUser = $"user-{Guid.NewGuid():N}";
        var token = BearerTestAuthHandler.Token(secretUser);
        var secretSearch = $"search-{Guid.NewGuid():N}";
        await using var client = await fixture.ConnectAsync(token);

        await client.CallToolAsync("clinic_patient", new Dictionary<string, object?> { ["searchText"] = secretSearch }, cancellationToken: Cancel);
        await client.CallToolAsync("clinic_start_visit", new Dictionary<string, object?> { ["patientId"] = 1 }, cancellationToken: Cancel);

        Assert.DoesNotContain(fixture.Logs, x => x.Text.Contains(token, StringComparison.Ordinal) || x.Text.Contains(secretSearch, StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.SpanTags, x => x.Value is { } value && (value.Contains(token, StringComparison.Ordinal) || value.Contains(secretSearch, StringComparison.Ordinal)));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var waited = TimeSpan.Zero; waited < TimeSpan.FromSeconds(10); waited += TimeSpan.FromMilliseconds(50))
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, Cancel);
        }

        Assert.Fail("The condition did not hold within 10 seconds.");
    }
}
