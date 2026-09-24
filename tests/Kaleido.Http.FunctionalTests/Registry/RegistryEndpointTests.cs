using System.Net;
using Kaleido.Http.Registry;
using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Registry;

[Collection(nameof(ProcessAspNetCoreSuite))]
public sealed class RegistryEndpointTests
{
    private readonly HttpClient _client;

    public RegistryEndpointTests(ProcessAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetRegistry_ReturnsOk()
    {
        var response = await _client.GetAsync("/kaleido/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRegistry_IncludesLocalProcessRegistrations()
    {
        var response = await _client.GetAsync("/kaleido/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registry =
            await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>();

        Assert.NotNull(registry);
        Assert.NotEmpty(registry.Processes);
    }
}
