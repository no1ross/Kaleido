using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Platform.Registry;

namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// <see cref="IKaleidoRegistryService"/> filters the hydrated central registry per caller,
/// so the MCP adapter's in-process view never exposes another caller's capabilities.
/// </summary>
public sealed class InProcessRegistryTests(
    AuthorizationAspNetCoreFixture fixture)
    : IClassFixture<AuthorizationAspNetCoreFixture>
{
    private async Task<KaleidoRegistry> ReadAsync(string url, string? user, params string[] roles)
    {
        using var request = user is null
            ? new HttpRequestMessage(HttpMethod.Get, url)
            : AuthorizationAspNetCoreFixture.AuthenticatedRequest(HttpMethod.Get, url, user, roles);

        using var response = await fixture.Client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsync<KaleidoRegistry>()
            ?? throw new InvalidOperationException("No registry.");
    }

    [Fact]
    public async Task TheServiceInProcess_FiltersPerCaller()
    {
        var viewer = await ReadAsync(AuthorizationAspNetCoreFixture.InProcessRegistryUrl, "alice", "viewer");
        var internalCaller = await ReadAsync(AuthorizationAspNetCoreFixture.InProcessRegistryUrl, "alice", "internal");

        static IEnumerable<string> Steps(KaleidoRegistry registry) =>
            registry.Processes.SelectMany(x => x.Steps ?? []).Select(x => x.Name);

        Assert.DoesNotContain(AuthorizationStepNames.InternalStep, Steps(viewer));
        Assert.Contains(AuthorizationStepNames.InternalStep, Steps(internalCaller));
        Assert.NotEqual(viewer.Revision, internalCaller.Revision);
    }
}
