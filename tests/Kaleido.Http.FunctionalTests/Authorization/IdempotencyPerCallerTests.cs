using System.Text.Json;

namespace Kaleido.Http.FunctionalTests.Authorization;

// #503: an Idempotency-Key belongs to its caller. Two users who happen to send the same key each run
// the step once and get their own answer; each one's repeat gets their own stored answer back.
// Catches keys scoped per service only (one user would get another user's process).
public sealed class IdempotencyPerCallerTests(
    AuthorizationAspNetCoreFixture fixture)
    : IClassFixture<AuthorizationAspNetCoreFixture>
{
    private const string OpenStepUrl = "/kaleido/processes/steps/authorizedopenstep";

    private async Task<Guid> StartAsAsync(string user, string key)
    {
        using var request = AuthorizationAspNetCoreFixture.AuthenticatedJson(OpenStepUrl, new { processStep = new { } }, user);
        request.Headers.Add(KaleidoIdempotencyHeaders.IdempotencyKey, key);

        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return body.RootElement.GetProperty("processId").GetGuid();
    }

    [Fact]
    public async Task TwoCallersWithTheSameKey_EachRunOnce_AndEachReplayIsTheirOwn()
    {
        var key = $"shared-{Guid.NewGuid():N}";

        var alice = await StartAsAsync("alice", key);
        var bob = await StartAsAsync("bob", key);

        Assert.NotEqual(alice, bob);
        Assert.Equal(alice, await StartAsAsync("alice", key));
        Assert.Equal(bob, await StartAsAsync("bob", key));
    }

    // #514: the scope's parts are framed, so a separator in a name cannot join two callers' keys - the
    // caller "alice" with the key "tenant:K" and the caller "alice:tenant" with the key "K" were one
    // admission when the scope was the string "caller:key".
    [Fact]
    public async Task ASeparatorInTheCallerOrTheKey_DoesNotJoinTwoCallersKeys()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var first = await StartAsAsync("alice", $"tenant:{suffix}");
        var second = await StartAsAsync("alice:tenant", suffix);

        Assert.NotEqual(first, second);
    }
}
