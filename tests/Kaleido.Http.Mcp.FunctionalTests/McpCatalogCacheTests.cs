using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// Two MCP hosts in one process (#464). Each keeps the tools it translated from its registry: hosts
/// with the same registry revision but different settings each list their own tools, and hosts with
/// the same settings each translate once and then reuse their own translation.
/// </summary>
public sealed class McpCatalogCacheTests
{
    private const string RegistryFetched = "Kaleido MCP registry read: fetched";
    private const string RegistryHit = "Kaleido MCP registry read: hit";

    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static async Task<string[]> ToolNamesAsync(McpClinicFixture host)
    {
        await using var client = await host.ConnectAsync(Nurse);

        return (await client.ListToolsAsync(cancellationToken: Cancel)).Select(x => x.Name).Order(StringComparer.Ordinal).ToArray();
    }

    private static int Count(McpClinicFixture host, string message) =>
        host.Logs.Count(x => x.Text.StartsWith(message, StringComparison.Ordinal));

    [Fact]
    public async Task TwoHostsWithTheSameRegistry_ButDifferentSettings_EachListTheirOwnTools()
    {
        var everyTool = new McpClinicFixture();
        var frontDoor = new FrontDoorFixture();

        try
        {
            await everyTool.InitializeAsync();
            await frontDoor.InitializeAsync();

            var first = await ToolNamesAsync(everyTool);
            var doors = await ToolNamesAsync(frontDoor);
            var again = await ToolNamesAsync(everyTool);

            Assert.Contains("clinic_close_visit", first);
            Assert.DoesNotContain("search_tools", first);
            Assert.Contains("search_tools", doors);
            Assert.DoesNotContain("clinic_close_visit", doors);
            Assert.Equal(first, again);
        }
        finally
        {
            await everyTool.DisposeAsync();
            await frontDoor.DisposeAsync();
        }
    }

    [Fact]
    public async Task TwoHostsWithTheSameRegistryAndSettings_EachTranslateOnce_AndReuseTheirOwnTranslation()
    {
        var a = new McpClinicFixture();
        var b = new McpClinicFixture();

        try
        {
            await a.InitializeAsync();
            await b.InitializeAsync();

            await ToolNamesAsync(a);
            await ToolNamesAsync(a);
            await ToolNamesAsync(b);
            await ToolNamesAsync(b);

            Assert.Equal((1, 1), (Count(a, RegistryFetched), Count(a, RegistryHit)));
            Assert.Equal((1, 1), (Count(b, RegistryFetched), Count(b, RegistryHit)));
        }
        finally
        {
            await a.DisposeAsync();
            await b.DisposeAsync();
        }
    }
}
