using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Registry.UnitTests;

// Mapping the control registry; what each endpoint answers is the functional tests' (ControlRegistryTests).
public sealed class ControlRegistryEndpointRouteBuilderExtensionsTests
    : SutFixture
{
    private static WebApplication CreateEndpoints(bool options = true, bool store = true)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = "registry" });
        builder.Services.AddSingleton(Mock.Of<IHttpRegistryCache>());
        builder.Services.AddSingleton<RegistrySnapshot>();

        if (options)
        {
            builder.Services.AddSingleton(new KaleidoRegistryOptions { HeartbeatInterval = TimeSpan.FromSeconds(5) });
        }

        if (store)
        {
            builder.Services.AddScoped(_ => Mock.Of<IKaleidoRegistryStore>());
        }

        return builder.Build();
    }

    [Fact]
    public void Map_WithoutAddControlRegistry_IsAConfigurationError()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateEndpoints(options: false).MapKaleidoControlRegistry());

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, exception.Code);
    }

    // The registry's state is chosen explicitly, as every Kaleido store (ADR 0028).
    [Fact]
    public void Map_WithoutAStore_IsAConfigurationError()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateEndpoints(store: false).MapKaleidoControlRegistry());

        Assert.Equal(ConfigurationErrorCodes.StoresNotChosen, exception.Code);
        Assert.Contains("AddInMemoryRegistryStore()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Map_MapsRegistration_Heartbeat_Deregistration_AndTheReads()
    {
        var app = CreateEndpoints();

        app.MapKaleidoControlRegistry();

        var routes = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>()
            .Select(x => (x.RoutePattern.RawText, x.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods.Single()))
            .ToList();
        Assert.Equal(
            [("/registry/instances", "POST"), ("/registry/instances/{instanceId:guid}/heartbeat", "PUT"), ("/registry/instances/{instanceId:guid}", "DELETE"), ("/registry/", "GET"), ("/registry/status", "GET"), ("/registry/availability", "GET"), ("/registry/capabilities", "GET")],
            routes);
    }

    // The host's registry is the store's view: aggregated, kept no longer than a heartbeat.
    [Fact]
    public void Map_ConfiguresTheSnapshot_AsTheStoresView()
    {
        var app = CreateEndpoints();

        app.MapKaleidoControlRegistry();

        var snapshot = app.Services.GetRequiredService<RegistrySnapshot>();
        Assert.True(snapshot.IsMapped);
        Assert.True(snapshot.Aggregate);
        Assert.Equal(TimeSpan.FromSeconds(5), snapshot.CacheTtl);
    }

    [Fact]
    public void Map_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ControlRegistryEndpointRouteBuilderExtensions.MapKaleidoControlRegistry(null!));
    }

    public static TheoryData<string, Type?, string> Descriptions() => new()
    {
        { ControlRegistryEndpointNames.Register, typeof(RegistryInstanceRegistration), "200 RegistryLease, 400, 403, 415, 500" },
        { ControlRegistryEndpointNames.Heartbeat, typeof(RegistryHeartbeat), "200 RegistryLease, 400, 403, 404, 415, 500" },
        { ControlRegistryEndpointNames.Deregister, null, "204, 403, 404, 500" },
        { ControlRegistryEndpointNames.Read, null, "200 KaleidoRegistry, 304, 500, 502 KaleidoRegistry" },
        { ControlRegistryEndpointNames.Status, null, "200 KaleidoRegistryStatus, 304, 403, 500" },
        { ControlRegistryEndpointNames.Availability, null, "200 KaleidoRegistryAvailability, 304, 403, 500" },
        { ControlRegistryEndpointNames.Capabilities, null, "200 KaleidoRegistry, 304, 403, 500" },
    };

    // #553, #564: each endpoint is described for OpenAPI as the process endpoints are - tag, summary,
    // description, what it accepts, and every answer: its success type, and each error a KaleidoErrorResponse.
    [Theory]
    [MemberData(nameof(Descriptions))]
    public void Map_DescribesEachEndpoint_WithEveryAnswer(string name, Type? accepts, string answers)
    {
        var app = CreateEndpoints();
        app.MapKaleidoControlRegistry();

        var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints), x => x.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == name);

        Assert.Equal(["Control registry"], endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        Assert.False(string.IsNullOrWhiteSpace(endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()?.Summary));
        Assert.False(string.IsNullOrWhiteSpace(endpoint.Metadata.GetMetadata<IEndpointDescriptionMetadata>()?.Description));
        Assert.Equal(accepts, endpoint.Metadata.GetMetadata<IAcceptsMetadata>()?.RequestType);
        var produced = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(x => x.Type != typeof(void) || x.StatusCode != 200)
            .DistinctBy(x => x.StatusCode)
            .OrderBy(x => x.StatusCode)
            .Select(x => x.Type is null || x.Type == typeof(void) || x.Type == typeof(KaleidoErrorResponse) ? $"{x.StatusCode}" : $"{x.StatusCode} {x.Type.Name}");
        Assert.Equal(answers, string.Join(", ", produced));
        Assert.All(
            endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>().Where(x => x.StatusCode is >= 400 and not 502),
            x => Assert.Equal(typeof(KaleidoErrorResponse), x.Type));
    }
}
