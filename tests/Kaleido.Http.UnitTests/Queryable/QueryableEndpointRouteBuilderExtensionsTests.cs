using Kaleido.Http.Queryable;
using Kaleido.Queryable.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.UnitTests.Queryable;

public sealed class QueryableEndpointRouteBuilderExtensionsTests
    : SutFixture
{
    [Fact]
    public void MapQueryable_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            QueryableEndpointRouteBuilderExtensions.MapQueryable(null!));
    }

    [Fact]
    public void MapQueryable_ReturnsRouteGroupBuilder_SoConventionsCompose()
    {
        var endpoints = CreateEndpoints();

        var group =
            endpoints.MapQueryable();

        Assert.IsType<RouteGroupBuilder>(group);
    }

    [Fact]
    public void MapQueryable_RegistersContextEndpoints()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapQueryable();

        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryContextEndpointName("test-context")));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view")));
    }

    [Fact]
    public void MapQueryable_UsesExpectedRoutes()
    {
        var endpoints = CreateEndpoints("data");

        endpoints.MapQueryable();

        Assert.Null(FindEndpointByRoute(endpoints, "/data/queryable/test-context/metadata"));
        Assert.NotNull(FindEndpointByRoute(endpoints, "/data/queryable/test-context/query"));
        Assert.NotNull(FindEndpointByRoute(endpoints, "/data/queryable/test-context/test-view/query"));
    }

    [Fact]
    public void MapQueryable_UsesDisplayNameTags()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapQueryable();

        var viewEndpoint = FindEndpoint(endpoints, QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view"))!;

        var viewTags = viewEndpoint.Metadata.GetMetadata<ITagsMetadata>();

        Assert.Contains("Test Context - Test View", viewTags!.Tags);
    }

    private static RouteEndpoint? FindEndpoint(IEndpointRouteBuilder endpoints, string name) =>
        endpoints.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .SingleOrDefault(x =>
                x.Metadata
                    .OfType<IEndpointNameMetadata>()
                    .Any(m => string.Equals(m.EndpointName, name, StringComparison.Ordinal)));

    private static RouteEndpoint? FindEndpointByRoute(IEndpointRouteBuilder endpoints, string route) =>
        endpoints.DataSources
            .SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .SingleOrDefault(x => string.Equals(Normalize(x.RoutePattern.RawText), Normalize(route), StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string? route) => (route ?? string.Empty).Trim().Trim('/');

    private static WebApplication CreateEndpoints(string serviceName = "kaleido")
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());

        builder.Services.AddSingleton(Mock.Of<IQueryableService>());
        builder.Services.AddSingleton<IQueryableRegistry>(CreateQueryableRegistry());
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName });
        return builder.Build();
    }

    private static IQueryableRegistry CreateQueryableRegistry()
    {
        var registry = new Mock<IQueryableRegistry>();
        registry.Setup(x => x.Registrations).Returns([
            new QueryableContextRegistryItem
            {
                ContextType = typeof(TestContext),
                Name = "Test-Context",
                Description = "Test Context",
                DisplayName = "Test Context",
                Version = "1.0.0",
                Source = "Unit Test",
                Kind = QueryContextKind.Direct,
                Views = [
                    new QueryableViewRegistryItem
                    {
                        QueryViewType = typeof(TestView),
                        ViewType = typeof(TestViewContract),
                        ViewParametersType = typeof(EmptyQueryViewParameters),
                        Name = "Test-View",
                        Description = "Test View",
                        DisplayName = "Test View",
                        Version = "1.0.0"
                    }
                ]
            }
        ]);
        return registry.Object;
    }

    public sealed class TestContext
    {
    }

    public sealed class TestView
    {
    }

    public sealed class TestViewContract
    {
    }

    public sealed class TestSource
    {
    }
}
