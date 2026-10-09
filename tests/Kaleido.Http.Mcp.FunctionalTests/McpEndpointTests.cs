using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// The MCP endpoint end to end: an SDK MCP client talks to the adapter, which talks to a real
/// Kaleido service over HTTP with the client's credentials.
/// </summary>
public sealed class McpEndpointTests(McpClinicFixture fixture)
    : IClassFixture<McpClinicFixture>
{
    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");
    private static readonly string Doctor = BearerTestAuthHandler.Token("dora", ClinicRoles.Doctor);

    private static readonly string[] Checkup = ["checkup"];
    private static readonly string[] NotUrgent = ["false"];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnAnonymousCaller_SeesNoTools()
    {
        await using var client = await fixture.ConnectAsync(authorization: null);

        var tools = await client.ListToolsAsync(cancellationToken: Cancel);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task Tools_FollowTheCallersAccess_AndNeverIncludeInformationSteps()
    {
        await using var nurse = await fixture.ConnectAsync(Nurse);
        await using var doctor = await fixture.ConnectAsync(Doctor);

        var nurseTools = (await nurse.ListToolsAsync(cancellationToken: Cancel)).Select(x => x.Name).ToArray();
        var doctorTools = (await doctor.ListToolsAsync(cancellationToken: Cancel)).Select(x => x.Name).ToArray();

        Assert.Contains("clinic_patient", nurseTools);
        Assert.Contains("clinic_start_visit", nurseTools);
        Assert.Contains("clinic_close_visit", nurseTools);
        Assert.Contains("answer_information_request", nurseTools);
        Assert.Contains("get_process_state", nurseTools);
        Assert.DoesNotContain("clinic_chart", nurseTools);
        Assert.DoesNotContain(nurseTools, x => x.Contains("visit_questions", StringComparison.Ordinal));

        Assert.Contains("clinic_chart", doctorTools);
    }

    [Fact]
    public async Task ToolDefinitions_CarryDescriptionsSchemasAndHints()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var tools = await client.ListToolsAsync(cancellationToken: Cancel);
        var search = tools.Single(x => x.Name == "clinic_patient").ProtocolTool;
        var start = tools.Single(x => x.Name == "clinic_start_visit").ProtocolTool;

        Assert.Contains("Patients registered at the clinic", search.Description, StringComparison.Ordinal);
        Assert.True(search.Annotations?.ReadOnlyHint);
        Assert.Contains("Name (contains)", search.InputSchema.GetProperty("properties").GetProperty("searchText").GetProperty("description").GetString(), StringComparison.Ordinal);

        Assert.False(start.Annotations?.ReadOnlyHint);
        var patientId = start.InputSchema.GetProperty("properties").GetProperty("patientId");
        Assert.Equal("integer", patientId.GetProperty("type").GetString());
        Assert.Contains("Patients search", patientId.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("patientId", start.InputSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public async Task TheServerInstructions_IncludeTheDefaultsAndTheAdditionalGuidance()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        Assert.Contains("informationRequest", client.ServerInstructions, StringComparison.Ordinal);
        Assert.Contains("A visit starts with a patient search.", client.ServerInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AQueryTool_ReturnsTheSourcesResults()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var result = await Call(client, "clinic_patient", new() { ["searchText"] = "Ava" });

        Assert.NotEqual(true, result.IsError);
        var names = Body(result)["results"]!.AsArray().Select(x => x!["name"]!.GetValue<string>()).ToArray();
        Assert.Equal(["Ava Carter"], names);
    }

    [Fact]
    public async Task WithElicitation_TheUserAnswersTheQuestions_AndTheProcessMovesOn()
    {
        ElicitRequestParams? asked = null;

        await using var client = await fixture.ConnectAsync(
            Nurse,
            (request, _) =>
            {
                asked = request;
                return ValueTask.FromResult(new ElicitResult
                {
                    Action = "accept",
                    Content = new Dictionary<string, JsonElement>
                    {
                        [VisitQuestions.Reason] = JsonSerializer.SerializeToElement("injury"),
                        [VisitQuestions.Urgent] = JsonSerializer.SerializeToElement(true)
                    }
                });
            });

        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = 1 }));
        var question = Assert.IsType<ElicitRequestParams>(asked);
        Assert.Contains("Visit intake", question.Message, StringComparison.Ordinal);
        Assert.Equal(
            "Why is the patient visiting today?",
            question.RequestedSchema?.Properties[VisitQuestions.Reason].Title);

        Assert.Equal(VisitQuestions.RequestId, started["answered"]![0]!.GetValue<string>());
        Assert.Equal("clinic_close_visit", started["next"]!["required"]!["tool"]!.GetValue<string>());
        Assert.Null(started["next"]!["informationRequest"]);

        var closed = Body(await Call(client, "clinic_close_visit", new()
        {
            ["processId"] = started["processId"]!.GetValue<string>(),
            ["note"] = "Ice and rest."
        }));

        Assert.Equal("completed", closed["outcome"]!.GetValue<string>());
        Assert.Contains("complete", closed["next"]!["note"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithoutElicitation_TheQuestionsComeBack_AndAnswerInformationRequestSubmitsThem()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = 2 }));
        var next = started["next"]!;

        Assert.Equal("answer_information_request", next["required"]!["tool"]!.GetValue<string>());
        Assert.Equal(VisitQuestions.RequestId, next["informationRequest"]!["informationRequestId"]!.GetValue<string>());
        Assert.Contains("Never answer for the user", next["instruction"]!.GetValue<string>(), StringComparison.Ordinal);

        var answered = Body(await Call(client, "answer_information_request", new()
        {
            ["service"] = "clinic",
            ["processId"] = started["processId"]!.GetValue<string>(),
            ["informationRequestId"] = VisitQuestions.RequestId,
            ["answers"] = new object[]
            {
                new { itemId = VisitQuestions.Reason, values = Checkup },
                new { itemId = VisitQuestions.Urgent, values = NotUrgent }
            }
        }));

        Assert.Equal("completed", answered["outcome"]!.GetValue<string>());
        Assert.Equal("clinic_close_visit", answered["next"]!["required"]!["tool"]!.GetValue<string>());
    }

    [Fact]
    public async Task AnswersToADifferentRequest_AreRejected_WithTheCurrentQuestions()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = 2 }));

        var result = await Call(client, "answer_information_request", new()
        {
            ["service"] = "clinic",
            ["processId"] = started["processId"]!.GetValue<string>(),
            ["informationRequestId"] = "something-else",
            ["answers"] = Array.Empty<object>()
        });

        Assert.True(result.IsError);
        var body = Body(result);
        Assert.Equal("mcp_invalid_arguments", body["code"]!.GetValue<string>());
        Assert.Equal(VisitQuestions.RequestId, body["details"]!["informationRequestId"]!.GetValue<string>());
    }

    [Fact]
    public async Task AStepThatNeedsAProcess_WithoutProcessId_IsAnArgumentError()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var result = await Call(client, "clinic_close_visit", new() { ["note"] = "x" });

        Assert.True(result.IsError);
        Assert.Equal("mcp_invalid_arguments", Body(result)["code"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetProcessState_ReportsThePendingQuestions()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = 1 }));

        var state = Body(await Call(client, "get_process_state", new()
        {
            ["service"] = "clinic",
            ["processId"] = started["processId"]!.GetValue<string>()
        }));

        Assert.Equal("awaitingInformation", state["state"]!.GetValue<string>());
        Assert.Equal(VisitQuestions.RequestId, state["next"]!["informationRequest"]!["informationRequestId"]!.GetValue<string>());
    }

    private static async Task<CallToolResult> Call(
        McpClient client,
        string tool,
        Dictionary<string, object?> arguments) =>
        await client.CallToolAsync(tool, arguments, cancellationToken: Cancel);

    private static JsonNode Body(CallToolResult result) =>
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().Single().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");
}
