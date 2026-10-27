using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kaleido.Http.Mcp.FunctionalTests.Infrastructure;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// Guided lookups end to end over MRTR (#452): the SDK client's user searches and picks a patient in
/// forms, the adapter fills the step's input from the chosen row, and the state that goes round trip
/// through the client between the forms is opaque, bound to the caller, and refused when changed.
/// </summary>
public sealed class McpLookupTests(LookupFixture fixture)
    : IClassFixture<LookupFixture>
{
    private const string Mrtr = "2026-07-28";
    private const string AssignBed = "clinic_assign_bed";
    private const string StatePrefix = "kaleido-mcp-lookup:";

    private static readonly string Nurse = BearerTestAuthHandler.Token("nina");
    private static readonly string OtherNurse = BearerTestAuthHandler.Token("nora");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static JsonNode Body(CallToolResult result) =>
        JsonNode.Parse(result.Content.OfType<TextContentBlock>().First().Text)
        ?? throw new InvalidOperationException("The tool returned no JSON.");

    // A user who searches for "Ava" and picks the first result.
    private static ValueTask<ElicitResult> SearchesAvaAndPicksTheFirst(ElicitRequestParams? request, CancellationToken cancellationToken)
    {
        var properties = request?.RequestedSchema?.Properties ?? new Dictionary<string, ElicitRequestParams.PrimitiveSchemaDefinition>();
        var content = properties.ContainsKey("search")
            ? new Dictionary<string, JsonElement> { ["search"] = JsonSerializer.SerializeToElement("Ava") }
            : new Dictionary<string, JsonElement> { ["result"] = JsonSerializer.SerializeToElement("0") };

        return ValueTask.FromResult(new ElicitResult { Action = "accept", Content = content });
    }

    private int AssignBedRuns() =>
        fixture.ServiceRequests.Count(x => x.Path.EndsWith("/assignbedstep", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public async Task ThePickedRow_FillsTheStepsInput_AndTheStateTheClientCarriesIsOpaque()
    {
        var states = new StateRewriter(state => state);
        await using var client = await fixture.ConnectAsync(Nurse, SearchesAvaAndPicksTheFirst, protocolVersion: Mrtr, outgoing: states);
        var before = AssignBedRuns();

        var result = await client.CallToolAsync(AssignBed, new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.NotEqual(true, result.IsError);
        Assert.Contains("\"patientId\":1", Body(result).ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(before + 1, AssignBedRuns());

        // The search state and the pick state went through the client; neither shows the rows or ids.
        Assert.Equal(2, states.Seen.Count);
        Assert.All(states.Seen, state =>
        {
            Assert.StartsWith(StatePrefix, state, StringComparison.Ordinal);
            Assert.DoesNotContain("Ava", state, StringComparison.Ordinal);
            Assert.DoesNotContain("patientId", Decoded(state), StringComparison.Ordinal);
        });
    }

    // A client that changes the state - here one character of its ciphertext - runs nothing.
    [Fact]
    public async Task AChangedState_IsRefused_AndNothingRuns()
    {
        var states = new StateRewriter(state => state[..^2] + (state[^2] == 'A' ? 'B' : 'A') + state[^1]);
        await using var client = await fixture.ConnectAsync(Nurse, SearchesAvaAndPicksTheFirst, protocolVersion: Mrtr, outgoing: states);
        var before = AssignBedRuns();

        var result = await client.CallToolAsync(AssignBed, new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.True(result.IsError);
        Assert.Equal(McpErrorCodes.InvalidArguments, Body(result)["code"]?.GetValue<string>());
        Assert.Equal(before, AssignBedRuns());
    }

    // A state made by the client itself - the readable form the adapter used before #452, with the
    // ids it would like to run with - is refused.
    [Fact]
    public async Task AStateTheClientMadeItself_IsRefused_AndNothingRuns()
    {
        var forged = StatePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(
            """{"tool":"clinic_assign_bed","group":"clinic/PatientSource","processId":null,"arguments":"{}","searchText":"Ava","candidates":[{"label":"Ava Carter","values":{"patientId":5}}]}"""));
        var states = new StateRewriter(_ => forged);
        await using var client = await fixture.ConnectAsync(Nurse, SearchesAvaAndPicksTheFirst, protocolVersion: Mrtr, outgoing: states);
        var before = AssignBedRuns();

        var result = await client.CallToolAsync(AssignBed, new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.True(result.IsError);
        Assert.Equal(McpErrorCodes.InvalidArguments, Body(result)["code"]?.GetValue<string>());
        Assert.Equal(before, AssignBedRuns());
    }

    // A state issued to one caller and replayed by another is refused.
    [Fact]
    public async Task AnotherCallersState_IsRefused_AndNothingRuns()
    {
        var first = new StateRewriter(state => state);
        await using (var nurse = await fixture.ConnectAsync(Nurse, SearchesAvaAndPicksTheFirst, protocolVersion: Mrtr, outgoing: first))
        {
            await nurse.CallToolAsync(AssignBed, new Dictionary<string, object?>(), cancellationToken: Cancel);
        }

        var replayed = first.Seen[^1];
        var states = new StateRewriter(_ => replayed);
        await using var other = await fixture.ConnectAsync(OtherNurse, SearchesAvaAndPicksTheFirst, protocolVersion: Mrtr, outgoing: states);
        var before = AssignBedRuns();

        var result = await other.CallToolAsync(AssignBed, new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.True(result.IsError);
        Assert.Equal(McpErrorCodes.InvalidArguments, Body(result)["code"]?.GetValue<string>());
        Assert.Equal(before, AssignBedRuns());
    }

    [Fact]
    public async Task AnOversizedState_IsRefused_AndNothingRuns()
    {
        var states = new StateRewriter(_ => StatePrefix + new string('A', 70_000));
        await using var client = await fixture.ConnectAsync(Nurse, SearchesAvaAndPicksTheFirst, protocolVersion: Mrtr, outgoing: states);
        var before = AssignBedRuns();

        var result = await client.CallToolAsync(AssignBed, new Dictionary<string, object?>(), cancellationToken: Cancel);

        Assert.True(result.IsError);
        Assert.Equal(McpErrorCodes.InvalidArguments, Body(result)["code"]?.GetValue<string>());
        Assert.Equal(before, AssignBedRuns());
    }

    // What a curious client could read from the state if it were plain Base64: nothing, it is not.
    private static string Decoded(string state)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(state[StatePrefix.Length..]));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    // Sees (and may replace) the lookup state the SDK client sends back on each MRTR retry.
    private sealed class StateRewriter(Func<string, string> rewrite) : DelegatingHandler
    {
        public List<string> Seen { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null &&
                JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken)) is JsonObject message &&
                message["params"]?["requestState"]?.GetValue<string>() is { } state &&
                state.StartsWith(StatePrefix, StringComparison.Ordinal))
            {
                Seen.Add(state);
                message["params"]!["requestState"] = rewrite(state);
                request.Content = new StringContent(message.ToJsonString(), Encoding.UTF8, "application/json");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
