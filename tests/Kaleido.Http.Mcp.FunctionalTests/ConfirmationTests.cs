using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// The confirmation round end to end (#439): with <c>RequireConfirmation</c> on, the clinic's close
/// visit (declared <c>RequiresConfirmation</c>) runs only after the SDK client's user accepts the
/// adapter's MRTR form, and never from a client that cannot be asked - one without forms, or an older
/// revision with no session over the stateless transport. (The in-session form is covered by the
/// invoker's unit tests.)
/// </summary>
public sealed class ConfirmationTests(ConfirmationFixture fixture)
    : IClassFixture<ConfirmationFixture>
{
    private const string Mrtr = "2026-07-28";
    private const string SessionElicitation = "2025-11-25";

    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonNode Body(CallToolResult result) =>
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().First().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");

    // A user who answers the visit intake, and answers the confirmation form with action/confirm.
    private static Func<ElicitRequestParams?, CancellationToken, ValueTask<ElicitResult>> User(
        List<ElicitRequestParams> confirmations,
        string action,
        bool confirm) =>
        (request, _) =>
        {
            if (request?.RequestedSchema?.Properties.ContainsKey("confirm") == true)
            {
                confirmations.Add(request);
                return ValueTask.FromResult(new ElicitResult
                {
                    Action = action,
                    Content = action == "accept" ? new Dictionary<string, JsonElement> { ["confirm"] = JsonSerializer.SerializeToElement(confirm) } : null
                });
            }

            return ValueTask.FromResult(new ElicitResult
            {
                Action = "accept",
                Content = new Dictionary<string, JsonElement>
                {
                    [VisitQuestions.Reason] = JsonSerializer.SerializeToElement("injury"),
                    [VisitQuestions.Urgent] = JsonSerializer.SerializeToElement(true)
                }
            });
        };

    // A visit whose intake is answered (through MRTR forms), waiting for close visit.
    private async Task<string> VisitReadyToCloseAsync()
    {
        await using var client = await fixture.ConnectAsync(Nurse, User([], "accept", confirm: true), protocolVersion: Mrtr);
        var started = Body(await client.CallToolAsync("clinic_start_visit", new Dictionary<string, object?> { ["patientId"] = 1 }, cancellationToken: Cancel));

        Assert.Equal("clinic_close_visit", started["next"]?["required"]?["tool"]?.GetValue<string>());
        return started["processId"]?.GetValue<string>() ?? throw new InvalidOperationException("No processId.");
    }

    // The agent adds its own `confirm: true`: it changes nothing.
    private static Task<CallToolResult> CloseAsync(McpClient client, string processId) =>
        client.CallToolAsync(
            "clinic_close_visit",
            new Dictionary<string, object?> { ["processId"] = processId, ["note"] = "Ice and rest.", ["confirm"] = true },
            cancellationToken: Cancel).AsTask();

    private async Task<string?> RequiredToolAsync(string processId)
    {
        await using var client = await fixture.ConnectAsync(Nurse);
        var state = Body(await client.CallToolAsync("get_process_state", new Dictionary<string, object?> { ["service"] = "clinic", ["processId"] = processId }, cancellationToken: Cancel));

        return state["next"]?["required"]?["tool"]?.GetValue<string>();
    }

    [Fact]
    public async Task Accepted_TheStepRuns_AfterAFormNamingItAndItsInputs()
    {
        var processId = await VisitReadyToCloseAsync();
        var confirmations = new List<ElicitRequestParams>();
        await using var client = await fixture.ConnectAsync(Nurse, User(confirmations, "accept", confirm: true), protocolVersion: Mrtr);

        var closed = Body(await CloseAsync(client, processId));

        Assert.Equal("completed", closed["outcome"]?.GetValue<string>());
        var form = Assert.Single(confirmations);
        Assert.Contains("Close visit", form.Message, StringComparison.Ordinal);
        Assert.Contains("cannot be undone", form.Message, StringComparison.Ordinal);
        Assert.Contains("Ice and rest.", form.Message, StringComparison.Ordinal);
        Assert.Null(await RequiredToolAsync(processId));
    }

    // Declined, or accepted with the box unchecked: nothing runs, and the process still needs the step.
    [Theory]
    [InlineData("decline", false)]
    [InlineData("cancel", false)]
    [InlineData("accept", false)]
    public async Task NotAccepted_NothingRuns(string action, bool confirm)
    {
        var processId = await VisitReadyToCloseAsync();
        var confirmations = new List<ElicitRequestParams>();
        await using var client = await fixture.ConnectAsync(Nurse, User(confirmations, action, confirm), protocolVersion: Mrtr);

        var closed = Body(await CloseAsync(client, processId));

        Assert.Equal("notRun", closed["outcome"]?.GetValue<string>());
        Assert.Single(confirmations);
        Assert.Equal("clinic_close_visit", await RequiredToolAsync(processId));
    }

    // #516: the confirmation state the client carries is protected. A client that sends back its own
    // state - here the same step with another note, readable and unsigned - is refused, and nothing runs.
    [Fact]
    public async Task AForgedConfirmationState_IsRefused_AndNothingRuns()
    {
        var processId = await VisitReadyToCloseAsync();
        var forged = LookupStateRewriter.ConfirmationPrefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { tool = "clinic_close_visit", processId, arguments = """{"processId":"PID","note":"Not what the user saw."}""".Replace("PID", processId, StringComparison.Ordinal) })));
        var states = new LookupStateRewriter(_ => forged, LookupStateRewriter.ConfirmationPrefix);
        await using var client = await fixture.ConnectAsync(Nurse, User([], "accept", confirm: true), protocolVersion: Mrtr, outgoing: states);

        var result = await CloseAsync(client, processId);

        Assert.True(result.IsError);
        Assert.Equal(McpErrorCodes.InvalidArguments, Body(result)["code"]?.GetValue<string>());
        Assert.Single(states.Seen);
        Assert.Equal("clinic_close_visit", await RequiredToolAsync(processId));
    }

    [Fact]
    public async Task AClientWithoutForms_IsRefused_AndNothingRuns()
    {
        var processId = await VisitReadyToCloseAsync();
        await using var client = await fixture.ConnectAsync(Nurse);

        var refused = await CloseAsync(client, processId);

        Assert.True(refused.IsError);
        Assert.Equal(McpErrorCodes.ConfirmationRequired, Body(refused)["code"]?.GetValue<string>());
        Assert.Equal("clinic_close_visit", await RequiredToolAsync(processId));
    }

    // An older revision that can show forms, but has no session over the stateless transport: the user
    // cannot be asked there, so the step is refused - never run without the form.
    [Fact]
    public async Task AnOlderRevision_IsNeverRunWithoutTheForm()
    {
        var processId = await VisitReadyToCloseAsync();
        var confirmations = new List<ElicitRequestParams>();
        await using var client = await fixture.ConnectAsync(Nurse, User(confirmations, "accept", confirm: true), protocolVersion: SessionElicitation);

        var result = await CloseAsync(client, processId);

        if (confirmations.Count == 0)
        {
            Assert.Equal(McpErrorCodes.ConfirmationRequired, Body(result)["code"]?.GetValue<string>());
            Assert.Equal("clinic_close_visit", await RequiredToolAsync(processId));
        }
        else
        {
            Assert.Equal("completed", Body(result)["outcome"]?.GetValue<string>());
        }
    }

    [Fact]
    public async Task TheToolDescription_SaysTheUserConfirmsInAForm()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var close = (await client.ListToolsAsync(cancellationToken: Cancel)).Single(x => x.Name == "clinic_close_visit").ProtocolTool;

        Assert.Contains("The user is asked to confirm in a form before it runs", close.Description, StringComparison.Ordinal);
    }
}
