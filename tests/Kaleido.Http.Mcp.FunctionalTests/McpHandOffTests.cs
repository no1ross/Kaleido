using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// A hand-off through the MCP adapter (#503): a clinic step moves the visit to the ward service, and
/// the tool result's <c>next</c> names the ward's tools - read from the ward's state - so the agent
/// continues there with the same process id. Catches a follow-up that stays on the originating
/// service (nothing would be next).
/// </summary>
public sealed class McpHandOffTests(HandOffFixture fixture)
    : IClassFixture<HandOffFixture>
{
    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    [Fact]
    public async Task AfterAHandOff_NextNamesTheTargetsTools_AndTheProcessContinuesThere()
    {
        var cancel = TestContext.Current.CancellationToken;
        await using var client = await fixture.ConnectAsync(Nurse);

        var moved = Body(await client.CallToolAsync("clinic_transfer_to_ward", new Dictionary<string, object?>(), cancellationToken: cancel));

        Assert.Equal("completed", moved["outcome"]!.GetValue<string>());
        Assert.Equal(McpClinicFixture.WardService, moved["handoff"]!.GetValue<string>());
        Assert.Equal(
            ["ward_discharge"],
            moved["next"]!["available"]!.AsArray().Select(x => x!["tool"]!.GetValue<string>()));

        var discharged = Body(await client.CallToolAsync(
            "ward_discharge",
            new Dictionary<string, object?> { ["processId"] = moved["processId"]!.GetValue<string>() },
            cancellationToken: cancel));

        Assert.Equal("completed", discharged["outcome"]!.GetValue<string>());
    }

    private static JsonNode Body(CallToolResult result) =>
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().First().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");
}
