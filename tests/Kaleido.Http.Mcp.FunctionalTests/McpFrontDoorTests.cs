using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// The front door end to end: a small tool list; an open step described in <c>next</c> and run with
/// <c>call_tool</c>; queries sent with the process id they name.
/// </summary>
public sealed class McpFrontDoorTests(FrontDoorFixture fixture)
    : IClassFixture<FrontDoorFixture>
{
    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private static readonly string[] Checkup = ["checkup"];
    private static readonly string[] NotUrgent = ["false"];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheToolList_IsTheFrontDoor()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var tools = (await client.ListToolsAsync(cancellationToken: Cancel)).Select(x => x.Name).ToArray();

        Assert.Equal(
            ["answer_information_request", "call_tool", "clinic_patient", "clinic_start_visit", "describe_tool", "get_process_state", "search_tools"],
            tools.Order(StringComparer.Ordinal));
        Assert.Contains("call_tool", client.ServerInstructions, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOpenStep_IsDescribedInNext_AndRunsWithCallTool()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

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
                new { itemId = VisitQuestions.Urgent, values = NotUrgent }
            }
        }));

        var required = answered["next"]!["required"]!;
        Assert.Equal("clinic_close_visit", required["tool"]!.GetValue<string>());
        Assert.NotNull(required["inputs"]!["properties"]!["note"]);
        Assert.True(required["hints"]!["destructive"]!.GetValue<bool>());
        Assert.Contains("call_tool", answered["next"]!["howToRun"]!.GetValue<string>(), StringComparison.Ordinal);

        var closed = Body(await Call(client, "call_tool", new()
        {
            ["name"] = "clinic_close_visit",
            ["arguments"] = new Dictionary<string, object?> { ["processId"] = processId, ["note"] = "Ice and rest." }
        }));

        Assert.Equal("completed", closed["outcome"]!.GetValue<string>());
    }

    [Fact]
    public async Task AQuery_IsSentWithTheProcessIdItNames()
    {
        var processId = Guid.NewGuid().ToString();
        await using var client = await fixture.ConnectAsync(Nurse);

        var result = Body(await Call(client, "clinic_patient", new() { ["processId"] = processId, ["searchText"] = "Ava" }));

        Assert.Equal(processId, result["processId"]!.GetValue<string>());
        Assert.Contains(fixture.ServiceProcessIds, x => x.Path.Contains("/queryable/", StringComparison.Ordinal) && x.ProcessId == processId);
    }

    [Fact]
    public async Task SearchTools_FindsAnUnlistedTool()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var found = Body(await Call(client, "search_tools", new() { ["text"] = "close visit" }));

        Assert.Equal("clinic_close_visit", found["tools"]![0]!["name"]!.GetValue<string>());
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
