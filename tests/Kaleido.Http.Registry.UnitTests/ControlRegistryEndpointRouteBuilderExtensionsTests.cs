using Microsoft.AspNetCore.Builder;
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
            [("/registry/instances", "POST"), ("/registry/instances/{instanceId:guid}/heartbeat", "PUT"), ("/registry/instances/{instanceId:guid}", "DELETE"), ("/registry/", "GET"), ("/registry/status", "GET")],
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
}
