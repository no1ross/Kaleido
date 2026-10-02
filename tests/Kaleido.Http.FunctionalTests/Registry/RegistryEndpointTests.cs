using System.Net;
using Kaleido.Http.Registry;
using Kaleido.Processor.AspNetCore.FunctionalTests.Fixtures;

namespace Kaleido.Processor.AspNetCore.FunctionalTests.Registry;

[Collection(nameof(ProcessorAspNetCoreSuite))]
public sealed class RegistryEndpointTests
{
    private readonly HttpClient _client;

    public RegistryEndpointTests(ProcessorAspNetCoreFixture fixture)
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
            await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options);

        Assert.NotNull(registry);
        Assert.NotEmpty(registry.Processes);
    }

    [Fact]
    public async Task GetRegistry_StampsFreshnessMetadata()
    {
        var response = await _client.GetAsync("/kaleido/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(response.Headers.ETag?.Tag ?? "");
        Assert.Equal("no-cache", response.Headers.CacheControl?.ToString());

        var registry =
            await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options);

        Assert.NotNull(registry);
        Assert.NotEqual(default, registry.GeneratedAt);
        Assert.False(string.IsNullOrEmpty(registry.Revision));
        Assert.False(registry.IsPartial);

        // Body Revision mirrors the ETag header value.
        Assert.Equal($"\"{registry.Revision}\"", response.Headers.ETag!.Tag);
    }

    [Fact]
    public async Task GetRegistry_IfNoneMatch_Returns304()
    {
        var first = await _client.GetAsync("/kaleido/registry");
        var etag = first.Headers.ETag!.Tag;

        var request = new HttpRequestMessage(HttpMethod.Get, "/kaleido/registry");
        request.Headers.IfNoneMatch.ParseAdd(etag);
        var second = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        Assert.Equal(etag, second.Headers.ETag?.Tag);
    }
}
