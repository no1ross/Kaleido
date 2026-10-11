using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// When a downstream service is down, the adapter still answers every request with a well-formed,
/// successful result: the tools of the services that answer, and tool results as usual for them.
/// </summary>
public sealed class McpUnavailableTests(DownstreamDownFixture fixture)
    : IClassFixture<DownstreamDownFixture>
{
    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListingTools_ServesTheServicesThatAnswer()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var tools = await client.ListToolsAsync(cancellationToken: Cancel);

        Assert.Contains(tools, x => x.Name == "clinic_patient");
        Assert.DoesNotContain(tools, x => x.Name.StartsWith("radiology_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CallingAToolOfAServiceThatAnswers_Works()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var result = await client.CallToolAsync("clinic_patient", new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(JsonNode.Parse(result.Content.OfType<TextContentBlock>().Single().Text)?["results"]);
    }

    [Fact]
    public async Task CallingAToolThatIsNotPublished_IsAnUnknownToolResult()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var result = await client.CallToolAsync("radiology_capture_scan", new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.True(result.IsError);
        Assert.Equal("mcp_unknown_tool", JsonNode.Parse(result.Content.OfType<TextContentBlock>().Single().Text)?["code"]?.GetValue<string>());
    }
}