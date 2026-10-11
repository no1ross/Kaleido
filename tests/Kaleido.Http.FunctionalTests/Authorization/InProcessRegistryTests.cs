using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Http.Registry;

namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// <see cref="IKaleidoRegistryService"/> called in process sees exactly what the registry endpoint
/// serves to the same caller: the same per-caller filtering, the same revision.
/// </summary>
public sealed class InProcessRegistryTests(
    AuthorizationAspNetCoreFixture fixture)
    : IClassFixture<AuthorizationAspNetCoreFixture>
{
    private const string RegistryUrl = "/kaleido/registry";

    private async Task<AggregatedRegistryResponse> ReadAsync(string url, string? user, params string[] roles)
    {
        using var request = user is null
            ? new HttpRequestMessage(HttpMethod.Get, url)
            : AuthorizationAspNetCoreFixture.AuthenticatedRequest(HttpMethod.Get, url, user, roles);

        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsync<AggregatedRegistryResponse>()
            ?? throw new InvalidOperationException("No registry.");
    }

    [Theory]
    [InlineData(null, new string[0])]
    [InlineData("alice", new[] { "viewer" })]
    [InlineData("alice", new[] { "internal" })]
    [InlineData("alice", new[] { "admin" })]
    public async Task TheServiceInProcess_MatchesTheEndpoint_ForTheSameCaller(string? user, string[] roles)
    {
        var endpoint = await ReadAsync(RegistryUrl, user, roles);
        var inProcess = await ReadAsync(AuthorizationAspNetCoreFixture.InProcessRegistryUrl, user, roles);

        Assert.Equal(endpoint.Revision, inProcess.Revision);
        Assert.Equal(
            endpoint.Processes.SelectMany(x => x.Steps ?? []).Select(x => x.Name).Order(),
            inProcess.Processes.SelectMany(x => x.Steps ?? []).Select(x => x.Name).Order());
    }

    [Fact]
    public async Task TheServiceInProcess_FiltersPerCaller()
    {
        var viewer = await ReadAsync(AuthorizationAspNetCoreFixture.InProcessRegistryUrl, "alice", "viewer");
        var internalCaller = await ReadAsync(AuthorizationAspNetCoreFixture.InProcessRegistryUrl, "alice", "internal");

        static IEnumerable<string> Steps(AggregatedRegistryResponse registry) =>
            registry.Processes.SelectMany(x => x.Steps ?? []).Select(x => x.Name);

        Assert.DoesNotContain(AuthorizationStepNames.InternalStep, Steps(viewer));
        Assert.Contains(AuthorizationStepNames.InternalStep, Steps(internalCaller));
        Assert.NotEqual(viewer.Revision, internalCaller.Revision);
    }
}
