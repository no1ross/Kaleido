using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// The contracts between the MCP adapter and the Kaleido service behind it, end to end: what a tool
/// advertises is what the service accepts, query arguments become the service's query, business
/// messages reach the user unchanged, a rejected step changes nothing, and the questions of an
/// information request travel by either form mechanism (session elicitation or MRTR).
/// </summary>
public sealed class McpContractTests(McpClinicFixture fixture)
    : IClassFixture<McpClinicFixture>
{
    private const string Mrtr = "2026-07-28";
    private const string SessionElicitation = "2025-11-25";

    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private static readonly string[] WardsAAndB = ["A", "B"];
    private static readonly int[] Forty = [40];
    private static readonly string[] WardA = ["A"];
    private static readonly string[] Checkup = ["checkup"];
    private static readonly string[] NotUrgent = ["false"];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AQueryTool_UsesTheFieldsAndOperatorsItAdvertises_AndTheServiceAppliesFiltersSortAndPage()
    {
        await using var client = await fixture.ConnectAsync(Nurse);
        var schema = (await client.ListToolsAsync(cancellationToken: Cancel)).Single(x => x.Name == "clinic_patient").ProtocolTool.InputSchema.GetProperty("properties");
        var filterFields = Enum(schema.GetProperty("filters").GetProperty("items").GetProperty("properties").GetProperty("field"));
        var operators = Enum(schema.GetProperty("filters").GetProperty("items").GetProperty("properties").GetProperty("operator"));
        var sortFields = Enum(schema.GetProperty("sort").GetProperty("items").GetProperty("properties").GetProperty("field"));

        Assert.Contains("Ward", filterFields);
        Assert.Contains("Age", filterFields);
        Assert.Contains("in", operators);
        Assert.Contains("greaterThanOrEqual", operators);
        Assert.Contains("Age", sortFields);

        // Wards A and B, aged 40 or more, oldest first, second page of two.
        var body = Body(await Call(client, "clinic_patient", new()
        {
            ["filters"] = new object[]
            {
                new { field = "Ward", @operator = "in", values = WardsAAndB },
                new { field = "Age", @operator = "greaterThanOrEqual", values = Forty },
            },
            ["sort"] = new object[] { new { field = "Age", direction = "descending" } },
            ["page"] = new { size = 2, offset = 1 },
        }));

        Assert.Equal(3, body["totalCount"]!.GetValue<int>());
        Assert.Equal(["Ben Ortiz", "Ava Carter"], body["results"]!.AsArray().Select(x => x!["name"]!.GetValue<string>()));
    }

    [Fact]
    public async Task AQueryTheServiceRefuses_IsAToolError_CarryingTheServicesReason()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var result = await Call(client, "clinic_patient", new()
        {
            ["filters"] = new object[] { new { field = "Ward", @operator = "contains", values = WardA } },
        });

        Assert.True(result.IsError);
        var body = Body(result);
        Assert.NotEqual("mcp_internal_error", body["code"]!.GetValue<string>());
        Assert.Contains("qry_unsupported_operator", body.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABusinessMessage_ReachesTheCaller_AndIsMarkedForTheUser()
    {
        await using var client = await fixture.ConnectAsync(Nurse);
        var processId = await OpenVisitReadyToCloseAsync(client);

        var result = await Call(client, "clinic_close_visit", new() { ["processId"] = processId, ["note"] = "Ice and rest." });

        var body = Body(result);
        Assert.Equal("completed", body["outcome"]!.GetValue<string>());
        Assert.Contains(CloseVisitHandler.ClosedMessage, body["messages"]!.ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain(CloseVisitHandler.ClosedMessage, body["diagnostics"]?.ToJsonString() ?? string.Empty, StringComparison.Ordinal);

        var forUser = result.Content.OfType<TextContentBlock>().Where(x => x.Annotations?.Audience?.Contains(Role.User) == true).ToArray();
        Assert.Contains(forUser, x => x.Text.Contains(CloseVisitHandler.ClosedMessage, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARejectedStep_ChangesNothing_AndTheReasonReachesTheUser()
    {
        await using var client = await fixture.ConnectAsync(Nurse);
        var processId = await OpenVisitReadyToCloseAsync(client);

        var result = await Call(client, "clinic_close_visit", new() { ["processId"] = processId, ["note"] = CloseVisitHandler.UnclearNote });

        var body = Body(result);
        Assert.Equal("rejected", body["outcome"]!.GetValue<string>());
        Assert.Contains(result.Content.OfType<TextContentBlock>(), x =>
            x.Annotations?.Audience?.Contains(Role.User) == true &&
            x.Text.Contains(CloseVisitHandler.UnclearMessage, StringComparison.Ordinal));

        var state = Body(await Call(client, "get_process_state", new() { ["service"] = "clinic", ["processId"] = processId }));
        Assert.NotEqual("complete", state["state"]!.GetValue<string>());

        var corrected = Body(await Call(client, "clinic_close_visit", new() { ["processId"] = processId, ["note"] = "Ice and rest." }));
        Assert.Equal("completed", corrected["outcome"]!.GetValue<string>());
    }

    // MRTR: the tool call returns input_required with the form; the SDK client shows it, then calls
    // the tool again with the answers and the opaque state - which the adapter must round-trip.
    [Fact]
    public async Task WithMrtr_TheFormGoesToTheUser_AndTheAnswersReachTheProcess()
    {
        const string protocolVersion = Mrtr;
        var forms = 0;

        await using var client = await fixture.ConnectAsync(
            Nurse,
            (request, _) =>
            {
                forms++;
                Assert.Equal("Why is the patient visiting today?", request?.RequestedSchema?.Properties[VisitQuestions.Reason].Title);
                return ValueTask.FromResult(new ElicitResult
                {
                    Action = "accept",
                    Content = new Dictionary<string, JsonElement>
                    {
                        [VisitQuestions.Reason] = JsonSerializer.SerializeToElement("checkup"),
                        [VisitQuestions.Urgent] = JsonSerializer.SerializeToElement(false),
                    },
                });
            },
            protocolVersion: protocolVersion);

        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = 1 }));

        Assert.Equal(1, forms);
        Assert.Equal(VisitQuestions.RequestId, started["answered"]![0]!.GetValue<string>());
        Assert.Equal("clinic_close_visit", started["next"]!["required"]!["tool"]!.GetValue<string>());

        var state = Body(await Call(client, "get_process_state", new() { ["service"] = "clinic", ["processId"] = started["processId"]!.GetValue<string>() }));
        Assert.NotEqual("awaitingInformation", state["state"]!.GetValue<string>());
    }

    // A client on an initialize-based revision that can show forms: over the adapter's stateless
    // HTTP transport there is no session to elicit in, so the questions must still reach the agent
    // (next.informationRequest) rather than be lost. See the functional-test audit (session path).
    [Fact]
    public async Task WithAnOlderRevision_TheQuestionsAreNeverLost()
    {
        var forms = 0;

        await using var client = await fixture.ConnectAsync(
            Nurse,
            (_, _) =>
            {
                forms++;
                return ValueTask.FromResult(new ElicitResult { Action = "decline" });
            },
            protocolVersion: SessionElicitation);

        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = 1 }));

        Assert.True(
            forms == 1 || started["next"]?["informationRequest"]?["informationRequestId"]?.GetValue<string>() == VisitQuestions.RequestId,
            started.ToJsonString());
    }

    [Fact]
    public async Task WhenTheUserDeclinesTheForm_NothingIsSubmitted_AndTheQuestionsStayPending()
    {
        await using var client = await fixture.ConnectAsync(
            Nurse,
            (_, _) => ValueTask.FromResult(new ElicitResult { Action = "decline" }),
            protocolVersion: Mrtr);

        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = 2 }));

        Assert.Null(started["answered"]);
        var state = Body(await Call(client, "get_process_state", new() { ["service"] = "clinic", ["processId"] = started["processId"]!.GetValue<string>() }));
        Assert.Equal("awaitingInformation", state["state"]!.GetValue<string>());
        Assert.Equal(VisitQuestions.RequestId, state["next"]!["informationRequest"]!["informationRequestId"]!.GetValue<string>());
    }

    private static async Task<string> OpenVisitReadyToCloseAsync(McpClient client)
    {
        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = 1 }));
        var processId = started["processId"]!.GetValue<string>();

        var answered = Body(await Call(client, "answer_information_request", new()
        {
            ["service"] = "clinic",
            ["processId"] = processId,
            ["informationRequestId"] = VisitQuestions.RequestId,
            ["answers"] = new object[]
            {
                new { itemId = VisitQuestions.Reason, values = Checkup },
                new { itemId = VisitQuestions.Urgent, values = NotUrgent },
            },
        }));

        Assert.Equal("completed", answered["outcome"]!.GetValue<string>());
        return processId;
    }

    private static string[] Enum(JsonElement property) =>
        property.GetProperty("enum").EnumerateArray().Select(x => x.GetString()!).ToArray();

    private static async Task<CallToolResult> Call(McpClient client, string tool, Dictionary<string, object?> arguments) =>
        await client.CallToolAsync(tool, arguments, cancellationToken: Cancel);

    private static JsonNode Body(CallToolResult result) =>
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().First().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");
}
