using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// When Kaleido cannot be read, the adapter still answers every request with a well-formed,
/// successful result: no tools, and a tool result explaining why a call did not run.
/// </summary>
public sealed class McpUnavailableTests(UnreachableRegistryFixture fixture)
    : IClassFixture<UnreachableRegistryFixture>
{
    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListingTools_ReturnsAnEmptyList()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var tools = await client.ListToolsAsync(cancellationToken: Cancel);

        Assert.Empty(tools);
    }

    [Fact]
    public async Task CallingATool_ReturnsAServiceUnavailableResult()
    {
        await using var client = await fixture.ConnectAsync(Nurse);

        var result = await client.CallToolAsync(
            "clinic_patient",
            new Dictionary<string, object?>(),
            cancellationToken: Cancel);

        Assert.True(result.IsError);
        var body = JsonNode.Parse(result.Content.OfType<TextContentBlock>().Single().Text);
        Assert.Equal("mcp_service_unavailable", body?["code"]?.GetValue<string>());
    }
}
