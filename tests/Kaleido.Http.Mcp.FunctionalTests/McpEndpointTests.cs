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
    private static readonly string[] Injury = ["injury"];
    private static readonly string[] IsUrgent = ["true"];
    private static readonly string[] Ladder = ["Fell off a ladder"];

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
        // A search-only field is explained where it appears, so the agent knows what it matches.
        Assert.Contains("Name (contains, The patient's full name)", search.InputSchema.GetProperty("properties").GetProperty("searchText").GetProperty("description").GetString(), StringComparison.Ordinal);

        Assert.False(start.Annotations?.ReadOnlyHint);
        var patientId = start.InputSchema.GetProperty("properties").GetProperty("patientId");
        Assert.Equal("integer", patientId.GetProperty("type").GetString());
        Assert.Contains("Patients search", patientId.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("patientId", start.InputSchema.GetProperty("required").EnumerateArray().Select(x => x.GetString()));

        // Declared behavior: start is an ordinary step, but initial - called again it starts another
        // process, so not idempotent (#440); close is destructive and asks for confirmation.
        Assert.False(start.Annotations?.DestructiveHint);
        Assert.False(start.Annotations?.IdempotentHint);
        var close = tools.Single(x => x.Name == "clinic_close_visit").ProtocolTool;
        Assert.True(close.Annotations?.DestructiveHint);
        Assert.Contains("Ask the user to confirm before calling.", close.Description, StringComparison.Ordinal);
    }

    // #463: with no ServerVersion set, initialize reports the host application's own version (here the
    // test host's entry assembly), without its +commit suffix.
    [Fact]
    public async Task Initialize_WithoutAServerVersion_ReportsTheHostApplicationsVersion()
    {
        var entry = System.Reflection.Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("No entry assembly.");
        var informational = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(entry)?.InformationalVersion;
        var expected = informational?.Split('+')[0] ?? entry.GetName().Version?.ToString();

        await using var client = await fixture.ConnectAsync(Nurse);

        Assert.Equal("kaleido", client.ServerInfo.Name);
        Assert.Equal(expected, client.ServerInfo.Version);
        Assert.DoesNotContain('+', client.ServerInfo.Version);
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

        // What the returned fields mean, from the registry, for the fields in the result (#213).
        Assert.Equal("The patient's full name.", Body(result)["fieldDescriptions"]?["name"]?.GetValue<string>());
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

    // Conditional questions (ADR 0024): the follow-up is asked only once the answer enables it, in a
    // second form; one submission reaches Kaleido.
    [Fact]
    public async Task WithElicitation_AConditionalQuestion_IsAskedInASecondForm_OnlyWhenEnabled()
    {
        var forms = new List<string[]>();

        await using var client = await fixture.ConnectAsync(
            Nurse,
            (request, _) =>
            {
                var asked = request?.RequestedSchema?.Properties?.Keys.ToArray() ?? [];
                forms.Add(asked);

                return ValueTask.FromResult(new ElicitResult
                {
                    Action = "accept",
                    Content = asked.Contains(VisitQuestions.WhatHappened)
                        ? new Dictionary<string, JsonElement> { [VisitQuestions.WhatHappened] = JsonSerializer.SerializeToElement("Fell off a ladder") }
                        : new Dictionary<string, JsonElement>
                        {
                            [VisitQuestions.Reason] = JsonSerializer.SerializeToElement("injury"),
                            [VisitQuestions.Urgent] = JsonSerializer.SerializeToElement(true)
                        }
                });
            });

        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = VisitQuestions.ConditionalPatientId }));

        Assert.Equal([[VisitQuestions.Reason, VisitQuestions.Urgent], [VisitQuestions.WhatHappened]], forms);
        Assert.Equal(VisitQuestions.RequestId, started["answered"]![0]!.GetValue<string>());
        Assert.Equal("clinic_close_visit", started["next"]!["required"]!["tool"]!.GetValue<string>());
    }

    // Chat: the agent is handed only the questions that apply now, never the conditions; the
    // follow-up comes back with a token once the answer enables it.
    [Fact]
    public async Task WithoutElicitation_AConditionalQuestion_ComesBackAfterTheAnswerThatEnablesIt()
    {
        await using var client = await fixture.ConnectAsync(Nurse);
        var started = Body(await Call(client, "clinic_start_visit", new() { ["patientId"] = VisitQuestions.ConditionalPatientId }));
        var processId = started["processId"]!.GetValue<string>();

        Assert.Equal(
            [VisitQuestions.Reason, VisitQuestions.Urgent],
            started["next"]!["informationRequest"]!["items"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()));
        Assert.DoesNotContain("enableWhen", started.ToJsonString(), StringComparison.Ordinal);

        var followUp = Body(await Call(client, "answer_information_request", new()
        {
            ["service"] = "clinic",
            ["processId"] = processId,
            ["informationRequestId"] = VisitQuestions.RequestId,
            ["answers"] = new object[]
            {
                new { itemId = VisitQuestions.Reason, values = Injury },
                new { itemId = VisitQuestions.Urgent, values = IsUrgent }
            }
        }));

        Assert.Equal(
            VisitQuestions.WhatHappened,
            Assert.Single(followUp["next"]!["informationRequest"]!["items"]!.AsArray())!["id"]!.GetValue<string>());

        var answered = Body(await Call(client, "answer_information_request", new()
        {
            ["service"] = "clinic",
            ["processId"] = processId,
            ["informationRequestId"] = VisitQuestions.RequestId,
            ["answers"] = new object[] { new { itemId = VisitQuestions.WhatHappened, values = Ladder } },
            ["answersToken"] = followUp["next"]!["answersToken"]!.GetValue<string>()
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
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().First().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");

    [Fact]
    public async Task Tools_CallTheServiceAsTheCaller_NeverWithTheHostsCredentials()
    {
        var nurse = BearerTestAuthHandler.Token($"nurse-{Guid.NewGuid():N}");
        await using var client = await fixture.ConnectAsync(nurse);

        await client.CallToolAsync("clinic_patient", new Dictionary<string, object?>(), cancellationToken: Cancel);
        await client.CallToolAsync("clinic_start_visit", new Dictionary<string, object?> { ["patientId"] = 1 }, cancellationToken: Cancel);

        var calls = fixture.ServiceRequests.Where(x => x.Authorization == nurse).ToArray();

        Assert.Contains(calls, x => x.Path.Contains("/queryable/", StringComparison.Ordinal));
        Assert.Contains(calls, x => x.Path.Contains("/processes/steps/", StringComparison.Ordinal));
        Assert.DoesNotContain(fixture.ServiceRequests, x => x.Authorization == McpClinicFixture.HostServiceToken);
    }

    // #402: the service a tool reaches learns the request came through MCP, and which tool started it.
    [Fact]
    public async Task ATool_TellsTheServiceItCameThroughMcp_AndWhichTool()
    {
        var nurse = BearerTestAuthHandler.Token($"nurse-{Guid.NewGuid():N}");
        await using var client = await fixture.ConnectAsync(nurse);

        await client.CallToolAsync("clinic_patient", new Dictionary<string, object?>(), cancellationToken: Cancel);

        var call = Assert.Single(fixture.ServiceOrigins, x => x.Authorization == nurse && x.Path.Contains("/queryable/", StringComparison.Ordinal));
        Assert.Equal(("mcp", "clinic_patient"), (call.Origin, call.OriginTool));
    }
}
