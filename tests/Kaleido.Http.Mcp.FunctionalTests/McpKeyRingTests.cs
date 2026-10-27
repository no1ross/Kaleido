using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// Two MCP instances of one deployment (#452). The guided-lookup state is protected with Data
/// Protection, so a form shown by one instance and answered on another (a load balancer) works only
/// when the instances share a key ring - as the deployment guide configures it - and when they do not,
/// the instance that is asked refuses the form and says why in its log.
/// </summary>
public sealed class McpKeyRingTests
{
    private const string Mrtr = "2026-07-28";
    private const string AssignBed = "clinic_assign_bed";

    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonNode Body(CallToolResult result) =>
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().First().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");

    // The search form instance A showed, answered on instance B: B's client sends A's state back for its
    // first retry (as a load balancer would route it), then carries on with B's own.
    private static async Task<CallToolResult> AnswerOnBTheFormAShowedAsync(LookupFixture a, LookupFixture b)
    {
        var shownByA = LookupStateRewriter.Unchanged();
        await using (var client = await a.ConnectAsync(Nurse, McpLookupTests.SearchesAvaAndPicksTheFirst, protocolVersion: Mrtr, outgoing: shownByA))
        {
            await client.CallToolAsync(AssignBed, new Dictionary<string, object?>(), cancellationToken: Cancel);
        }

        var answeredOnB = new LookupStateRewriter((retry, state) => retry == 0 ? shownByA.Seen[0] : state);
        await using var other = await b.ConnectAsync(Nurse, McpLookupTests.SearchesAvaAndPicksTheFirst, protocolVersion: Mrtr, outgoing: answeredOnB);

        return await other.CallToolAsync(AssignBed, new Dictionary<string, object?>(), cancellationToken: Cancel);
    }

    [Fact]
    public async Task WithASharedKeyRing_AFormShownByOneInstance_IsAnsweredOnAnother()
    {
        var keys = Directory.CreateTempSubdirectory("kaleido-keys-");
        var a = new LookupFixture(keys.FullName);
        var b = new LookupFixture(keys.FullName);

        try
        {
            await a.InitializeAsync();
            await b.InitializeAsync();

            var result = await AnswerOnBTheFormAShowedAsync(a, b);

            Assert.NotEqual(true, result.IsError);
            Assert.Contains("\"patientId\":1", Body(result).ToJsonString(), StringComparison.Ordinal);
            Assert.Equal(1, b.AssignBedRuns());
            Assert.DoesNotContain(b.Logs, x => x.Level == LogLevel.Warning && x.Text.Contains("key ring", StringComparison.Ordinal));
        }
        finally
        {
            await a.DisposeAsync();
            await b.DisposeAsync();
            keys.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WithSeparateKeyRings_AFormShownByOneInstance_IsRefusedOnAnother_AndTheCauseIsLogged()
    {
        var keysOfA = Directory.CreateTempSubdirectory("kaleido-keys-");
        var keysOfB = Directory.CreateTempSubdirectory("kaleido-keys-");
        var a = new LookupFixture(keysOfA.FullName);
        var b = new LookupFixture(keysOfB.FullName);

        try
        {
            await a.InitializeAsync();
            await b.InitializeAsync();

            var result = await AnswerOnBTheFormAShowedAsync(a, b);

            Assert.True(result.IsError);
            Assert.Equal(McpErrorCodes.InvalidArguments, Body(result)["code"]?.GetValue<string>());
            Assert.Contains("start the search again", Body(result).ToJsonString(), StringComparison.Ordinal);
            Assert.Equal(0, b.AssignBedRuns());
            Assert.Contains(b.Logs, x => x.Level == LogLevel.Warning && x.Text.Contains("do not share one key ring", StringComparison.Ordinal));
        }
        finally
        {
            await a.DisposeAsync();
            await b.DisposeAsync();
            keysOfA.Delete(recursive: true);
            keysOfB.Delete(recursive: true);
        }
    }

    // One key folder but two application names: Data Protection keeps the applications' payloads apart,
    // so the instances do not share a key ring after all - an easy deployment mistake, refused the same way.
    [Fact]
    public async Task WithOneKeyFolderButDifferentApplicationNames_AFormShownByOneInstance_IsRefusedOnAnother()
    {
        var keys = Directory.CreateTempSubdirectory("kaleido-keys-");
        var a = new LookupFixture(keys.FullName, applicationName: "clinic");
        var b = new LookupFixture(keys.FullName, applicationName: "clinic-copy");

        try
        {
            await a.InitializeAsync();
            await b.InitializeAsync();

            var result = await AnswerOnBTheFormAShowedAsync(a, b);

            Assert.True(result.IsError);
            Assert.Equal(McpErrorCodes.InvalidArguments, Body(result)["code"]?.GetValue<string>());
            Assert.Equal(0, b.AssignBedRuns());
        }
        finally
        {
            await a.DisposeAsync();
            await b.DisposeAsync();
            keys.Delete(recursive: true);
        }
    }

    // A host that configured no key storage of its own is told at startup (outside Development); one
    // that configured it is not.
    [Fact]
    public async Task AtStartup_AHostWithoutKeyStorage_IsWarned_AndOneWithIt_IsNot()
    {
        var keys = Directory.CreateTempSubdirectory("kaleido-keys-");
        var withoutKeyStorage = new LookupFixture();
        var withKeyStorage = new LookupFixture(keys.FullName);

        try
        {
            await withoutKeyStorage.InitializeAsync();
            await withKeyStorage.InitializeAsync();

            Assert.Contains(withoutKeyStorage.Logs, x => x.Level == LogLevel.Warning && x.Text.Contains("no Data Protection key storage is configured", StringComparison.Ordinal));
            Assert.DoesNotContain(withKeyStorage.Logs, x => x.Text.Contains("no Data Protection key storage is configured", StringComparison.Ordinal));
        }
        finally
        {
            await withoutKeyStorage.DisposeAsync();
            await withKeyStorage.DisposeAsync();
            keys.Delete(recursive: true);
        }
    }
}
