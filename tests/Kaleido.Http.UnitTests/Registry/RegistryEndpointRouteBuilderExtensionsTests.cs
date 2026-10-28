using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Http.Registry.Contracts;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace Kaleido.Http.Registry.UnitTests;

public sealed class RegistryEndpointRouteBuilderExtensionsTests
    : SutFixture
{
    private const string Revision = "abc123";

    private readonly Mock<IKaleidoRegistryService> registry = new();

    public RegistryEndpointRouteBuilderExtensionsTests()
    {
        registry
            .Setup(x => x.GetForCallerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AggregatedRegistryResponse { Revision = Revision });
    }

    [Fact]
    public void MapRegistry_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            RegistryEndpointRouteBuilderExtensions.MapRegistry(null!, new KaleidoHttpMapOptions()));
    }

    [Fact]
    public void MapRegistry_WhenAggregatingWithoutClients_ThrowsAConfigurationError()
    {
        var endpoints = CreateEndpoints();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            endpoints.MapRegistry(new KaleidoHttpMapOptions { AggregateRegistry = true }));

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, exception.Code);
        Assert.Null(FindEndpoint(endpoints));
    }

    [Fact]
    public void MapRegistry_WhenAggregatingWithClients_MapsTheAggregatingEndpoint()
    {
        var endpoints = CreateEndpoints(clients: true);

        var group = endpoints.MapRegistry(new KaleidoHttpMapOptions { AggregateRegistry = true });

        Assert.IsType<RouteGroupBuilder>(group);
        var endpoint = FindEndpoint(endpoints);
        Assert.NotNull(endpoint);
        Assert.Equal("/test/registry", "/" + endpoint.RoutePattern.RawText?.TrimStart('/'));
        Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            x => x.StatusCode == StatusCodes.Status502BadGateway && x.Type == typeof(AggregatedRegistryResponse));
        var description = endpoint.Metadata.GetMetadata<IEndpointDescriptionMetadata>()!.Description;
        Assert.Contains("and configured downstream clients.", description, StringComparison.Ordinal);
        Assert.Contains("?strict returns HTTP 502", description, StringComparison.Ordinal);
        Assert.Contains("registered via AddHttpClients()", description, StringComparison.Ordinal);
    }

    [Fact]
    public void MapRegistry_WithoutAggregation_MapsALocalOnlyEndpoint()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapRegistry(new KaleidoHttpMapOptions());

        var endpoint = FindEndpoint(endpoints);
        Assert.NotNull(endpoint);
        Assert.DoesNotContain(
            endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            x => x.StatusCode == StatusCodes.Status502BadGateway);
        Assert.Contains("Registry", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        var description = endpoint.Metadata.GetMetadata<IEndpointDescriptionMetadata>()!.Description;
        Assert.Contains("from this service. ", description, StringComparison.Ordinal);
        Assert.Contains("local-only registry request returns HTTP 200", description, StringComparison.Ordinal);
        Assert.Contains("enable AggregateRegistry and call AddHttpClients()", description, StringComparison.Ordinal);
    }

    [Fact]
    public void MapRegistry_ConfiguresTheSharedSnapshot()
    {
        var endpoints = CreateEndpoints(snapshot: true, clients: true);
        var mapOptions = new KaleidoHttpMapOptions
        {
            AggregateRegistry = true,
            RegistryCacheTtl = TimeSpan.FromMinutes(5),
            RegistryRefreshCooldown = TimeSpan.FromSeconds(7),
            RegistryDownstreamMaxParallelism = 3,
        };

        endpoints.MapRegistry(mapOptions);

        var snapshot = endpoints.Services.GetRequiredService<RegistrySnapshot>();
        Assert.True(snapshot.IsMapped);
        Assert.True(snapshot.Aggregate);
        Assert.Equal(TimeSpan.FromMinutes(5), snapshot.CacheTtl);
        Assert.Equal(TimeSpan.FromSeconds(7), snapshot.RefreshCooldown);
        Assert.Equal(3, snapshot.DownstreamMaxParallelism);
    }

    [Fact]
    public async Task RegistryEndpoint_WithoutTheRegistryService_FailsClosed()
    {
        var endpoints = CreateEndpoints(registryService: false);
        endpoints.MapRegistry(new KaleidoHttpMapOptions());

        var exception = await Assert.ThrowsAsync<KaleidoConfigurationException>(() => InvokeAsync(endpoints));

        Assert.Equal(ConfigurationErrorCodes.AuthenticationNotConfigured, exception.Code);
    }

    [Fact]
    public async Task RegistryEndpoint_AnswersOkWithTheRegistryAndFreshnessHeaders()
    {
        var endpoints = CreateEndpoints();
        endpoints.MapRegistry(new KaleidoHttpMapOptions { RegistryCacheTtl = null });

        var context = await InvokeAsync(endpoints);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal($"\"{Revision}\"", context.Response.Headers.ETag.ToString());
        Assert.Equal("private, no-cache", context.Response.Headers.CacheControl.ToString());
        Assert.Equal(Revision, ReadJson(context).GetProperty("revision").GetString());
        registry.Verify(x => x.GetForCallerAsync(false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegistryEndpoint_WithACacheTtl_AdvertisesTheMaxAge()
    {
        var endpoints = CreateEndpoints();
        endpoints.MapRegistry(new KaleidoHttpMapOptions { RegistryCacheTtl = TimeSpan.FromSeconds(90.7) });

        var context = await InvokeAsync(endpoints);

        Assert.Equal("private, max-age=90", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task RegistryEndpoint_WithRefresh_AsksForARebuild()
    {
        var endpoints = CreateEndpoints();
        endpoints.MapRegistry(new KaleidoHttpMapOptions());

        await InvokeAsync(endpoints, query: "?refresh");

        registry.Verify(x => x.GetForCallerAsync(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("\"abc123\"")]
    [InlineData("*")]
    public async Task RegistryEndpoint_WhenIfNoneMatchMatches_AnswersNotModified(string ifNoneMatch)
    {
        var endpoints = CreateEndpoints();
        endpoints.MapRegistry(new KaleidoHttpMapOptions());

        var context = await InvokeAsync(endpoints, ifNoneMatch: new StringValues(["\"other\"", ifNoneMatch]));

        Assert.Equal(StatusCodes.Status304NotModified, context.Response.StatusCode);
        Assert.Equal($"\"{Revision}\"", context.Response.Headers.ETag.ToString());
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task RegistryEndpoint_WhenIfNoneMatchDiffers_AnswersOk()
    {
        var endpoints = CreateEndpoints();
        endpoints.MapRegistry(new KaleidoHttpMapOptions());

        var context = await InvokeAsync(endpoints, ifNoneMatch: "\"stale\"");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task RegistryEndpoint_Strict_WithClientErrors_AnswersBadGatewayWithTheBody()
    {
        registry
            .Setup(x => x.GetForCallerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AggregatedRegistryResponse
            {
                Revision = Revision,
                IsPartial = true,
                ClientErrors = [new RegistryClientError { ClientName = "radiology", ClientType = "Registry", Reason = "down" }],
            });
        var endpoints = CreateEndpoints(clients: true);
        endpoints.MapRegistry(new KaleidoHttpMapOptions { AggregateRegistry = true });

        var context = await InvokeAsync(endpoints, query: "?strict");

        Assert.Equal(StatusCodes.Status502BadGateway, context.Response.StatusCode);
        var error = Assert.Single(ReadJson(context).GetProperty("clientErrors").EnumerateArray());
        Assert.Equal("radiology", error.GetProperty("clientName").GetString());
    }

    [Fact]
    public async Task RegistryEndpoint_WithClientErrorsButNotStrict_AnswersOk()
    {
        registry
            .Setup(x => x.GetForCallerAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AggregatedRegistryResponse
            {
                Revision = Revision,
                IsPartial = true,
                ClientErrors = [new RegistryClientError { ClientName = "radiology", ClientType = "Registry", Reason = "down" }],
            });
        var endpoints = CreateEndpoints(clients: true);
        endpoints.MapRegistry(new KaleidoHttpMapOptions { AggregateRegistry = true });

        var context = await InvokeAsync(endpoints);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(ReadJson(context).GetProperty("isPartial").GetBoolean());
    }

    [Fact]
    public async Task RegistryEndpoint_Strict_WithoutClientErrors_AnswersOk()
    {
        var endpoints = CreateEndpoints();
        endpoints.MapRegistry(new KaleidoHttpMapOptions());

        var context = await InvokeAsync(endpoints, query: "?strict");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication endpoints,
        string? query = null,
        StringValues ifNoneMatch = default)
    {
        var endpoint = FindEndpoint(endpoints)!;
        var context = new DefaultHttpContext
        {
            RequestServices = endpoints.Services.CreateScope().ServiceProvider,
        };
        context.Response.Body = new MemoryStream();

        if (query is not null)
        {
            context.Request.QueryString = new QueryString(query);
        }

        if (!StringValues.IsNullOrEmpty(ifNoneMatch))
        {
            context.Request.Headers.IfNoneMatch = ifNoneMatch;
        }

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static JsonElement ReadJson(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement.Clone();
    }

    private static RouteEndpoint? FindEndpoint(IEndpointRouteBuilder endpoints) =>
        endpoints.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .SingleOrDefault(x =>
                x.Metadata
                    .OfType<IEndpointNameMetadata>()
                    .Any(m => string.Equals(m.EndpointName, RegistryEndpointNames.RegistryEndpointName, StringComparison.Ordinal)));

    private WebApplication CreateEndpoints(
        bool clients = false,
        bool snapshot = false,
        bool registryService = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = "test" });

        if (clients)
        {
            builder.Services.AddSingleton(Mock.Of<IKaleidoClientCatalog>());
        }

        if (snapshot)
        {
            builder.Services.AddSingleton(Mock.Of<IHttpRegistryCache>());
            builder.Services.AddSingleton<RegistrySnapshot>();
        }

        if (registryService)
        {
            builder.Services.AddSingleton(registry.Object);
        }

        return builder.Build();
    }
}
