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
        using var response = await _client.GetAsync("/kaleido/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRegistry_IncludesLocalProcessRegistrations()
    {
        using var response = await _client.GetAsync("/kaleido/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registry =
            await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options);

        Assert.NotNull(registry);
        Assert.NotEmpty(registry.Processes);
    }

    [Fact]
    public async Task GetRegistry_StampsFreshnessMetadata()
    {
        using var response = await _client.GetAsync("/kaleido/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(response.Headers.ETag?.Tag ?? "");
        // Default check interval 30 s; private - the registry is filtered per caller.
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromSeconds(30), response.Headers.CacheControl?.MaxAge);

        var registry =
            await response.Content.ReadFromJsonAsync<AggregatedRegistryResponse>(KaleidoJsonOptions.Options);

        Assert.NotNull(registry);
        Assert.NotEqual(default, registry.GeneratedAt);
        Assert.False(string.IsNullOrEmpty(registry.Revision));
        Assert.False(registry.IsPartial);

        // Body Revision mirrors the ETag header value.
        Assert.Equal($"\"{registry.Revision}\"", response.Headers.ETag!.Tag);
    }

    // The wire contract: what the implementer declared about a step, and the classifications of fields.
    [Fact]
    public async Task GetRegistry_PublishesTheDeclaredStepBehaviorAndFieldClassifications()
    {
        using var response = await _client.GetAsync("/kaleido/registry");
        var registry = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        var steps = registry["processes"]!.AsArray().SelectMany(x => x!["steps"]!.AsArray()).ToArray();
        var merge = steps.Single(x => x!["name"]!.GetValue<string>() == nameof(Processor.Infrastructure.RuntimeMergeStep))!;
        var root = steps.Single(x => x!["name"]!.GetValue<string>() == nameof(Processor.Infrastructure.RuntimeRootStep))!;

        Assert.True(merge["destructive"]!.GetValue<bool>());
        Assert.True(merge["external"]!.GetValue<bool>());
        Assert.True(merge["requiresConfirmation"]!.GetValue<bool>());
        Assert.False(root["destructive"]!.GetValue<bool>());
        Assert.False(root["requiresConfirmation"]!.GetValue<bool>());

        var source = registry["queryables"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "FunctionalRecordContextSource")!;
        var name = source["fields"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "Name")!;
        Assert.Equal(["functional:sensitive"], name["classifications"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public async Task GetRegistry_IfNoneMatch_Returns304()
    {
        using var first = await _client.GetAsync("/kaleido/registry");
        var etag = first.Headers.ETag!.Tag;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/kaleido/registry");
        request.Headers.IfNoneMatch.ParseAdd(etag);
        using var second = await _client.SendAsync(request);

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
                            o.AuthorizationMode = KaleidoAuthorizationMode.None;
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
