using System.Net;
using Kaleido.Http.Client;
using Kaleido.Http.FunctionalTests.Processor;
using Kaleido.Http.FunctionalTests.Processor.Fixtures;
using Kaleido.Http.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Registry;

[Collection(nameof(ProcessorAspNetCoreSuite))]
public sealed class RegistryEndpointTests(ProcessorAspNetCoreFixture fixture)
{
    private readonly HttpClient _client = fixture.Client;

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

    [Fact]
    public async Task GetRegistry_WhenDownstreamFails_DefaultAndStrictKeepPartialBody()
    {
        var handler = new UnavailableRegistryHandler();
        using var downstream = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var host = await StartWithUnavailableDownstreamAsync(downstream);
        using var client = host.GetTestClient();

        using var defaultResponse = await client.GetAsync("/kaleido/registry");
        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        var partial = await defaultResponse.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options);
        Assert.NotNull(partial);
        Assert.True(partial.IsPartial);
        Assert.NotEmpty(partial.Processes);
        var error = Assert.Single(partial.ClientErrors);
        Assert.Equal("unavailable", error.ClientName);
        Assert.Equal("Registry", error.ClientType);

        using var strictResponse = await client.GetAsync("/kaleido/registry?strict");
        Assert.Equal(HttpStatusCode.BadGateway, strictResponse.StatusCode);
        var strict = await strictResponse.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options);
        Assert.NotNull(strict);
        Assert.True(strict.IsPartial);
        Assert.Equal(partial.Processes.Count, strict.Processes.Count);
        Assert.Equal(error.ClientName, Assert.Single(strict.ClientErrors).ClientName);
        Assert.Equal(2, handler.RequestCount);
    }

    private static Task<IHost> StartWithUnavailableDownstreamAsync(HttpClient downstream) =>
        new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddRouting();
                    var config = new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Kaleido:Clients:unavailable:BaseUrl"] = "http://localhost/",
                            ["Kaleido:Clients:unavailable:RoutePrefix"] = "unavailable"
                        })
                        .Build();

                    services.AddKaleido(config, o =>
                        {
                            o.ServiceName = "kaleido";
                            o.Assemblies = [typeof(ProcessorAspNetCoreFixture).Assembly];
                        })
                        .AddHttp()
                        .AddHttpClients();
                    services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory(downstream));
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp(o => o.AggregateRegistry = true));
                });
            })
            .StartAsync();

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            name == "unavailable" ? client : throw new ArgumentException("Unknown client name.", nameof(name));
    }

    private sealed class UnavailableRegistryHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }
}
