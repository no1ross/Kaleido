using Kaleido.Queryable.AspNetCore;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Kaleido.Queryable.UnitTests.AspNetCore;

public sealed class QueryableEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public void MapQueryable_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            QueryableEndpointRouteBuilderExtensions.MapQueryable(null!));
    }

    [Fact]
    public void MapQueryable_RegistersCatalogRegistryAndContextEndpoints()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapQueryable();

        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.CatalogEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.RegistryEndpointName));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryContextMetadataEndpointName("test-context")));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryContextEndpointName("test-context")));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view")));
    }

    [Fact]
    public void MapQueryable_UsesExpectedRoutes()
    {
        var options = new QueryableRouteOptions
        {
            RoutePrefix = "/data",
            MetadataRoute = "schema",
            QueryRoute = "execute"
        };

        var endpoints = CreateEndpoints(options);

        endpoints.MapQueryable();

        Assert.NotNull(FindEndpointByRoute(endpoints, "/data/queryable"));
        Assert.NotNull(FindEndpointByRoute(endpoints, "/data/queryable/registry"));
        Assert.NotNull(FindEndpointByRoute(endpoints, "/data/queryable/test-context/schema"));
        Assert.NotNull(FindEndpointByRoute(endpoints, "/data/queryable/test-context/execute"));
        Assert.NotNull(FindEndpointByRoute(endpoints, "/data/queryable/test-context/test-view/execute"));
    }

    [Fact]
    public void MapQueryable_UsesDisplayNameTags()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapQueryable();

        var metadataEndpoint = FindEndpoint(endpoints, QueryableEndpointNames.QueryContextMetadataEndpointName("test-context"))!;
        var viewEndpoint = FindEndpoint(endpoints, QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view"))!;

        var metadataTags = metadataEndpoint.Metadata.GetMetadata<ITagsMetadata>();
        var viewTags = viewEndpoint.Metadata.GetMetadata<ITagsMetadata>();

        Assert.Contains("Test Context", metadataTags!.Tags);
        Assert.Contains("Test Context - Test View", viewTags!.Tags);
    }

    [Fact]
    public void MapQueryView_ThrowsWhenContextIsMissing()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        using var app = builder.Build();

        var contextRegistry = new Mock<IQueryContextRegistry>();
        contextRegistry.Setup(x => x.GetRegistration(typeof(TestContext))).Throws(new KeyNotFoundException("missing"));

        var exception = Assert.Throws<KeyNotFoundException>(() =>
            app.MapQueryView(
                contextRegistry.Object,
                CreateViewRegistration(),
                new QueryableRouteOptions()));

        Assert.Equal("missing", exception.Message);
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

    private static WebApplication CreateEndpoints(QueryableRouteOptions? options = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        var routeOptions = options ?? new QueryableRouteOptions();

        builder.Services.AddSingleton(Mock.Of<IQueryableService>());
        builder.Services.AddSingleton<IQueryContextRegistry>(CreateContextRegistry());
        builder.Services.AddSingleton<IQueryViewRegistry>(CreateViewRegistry());
        builder.Services.AddSingleton<IDelegatedQueryViewRegistry>(CreateDelegatedViewRegistry());
        builder.Services.AddSingleton<IQueryableRegistry>(CreateQueryableRegistry());
        builder.Services.AddSingleton(routeOptions);
        return builder.Build();
    }

    private static IQueryContextRegistry CreateContextRegistry()
    {
        var registry = new Mock<IQueryContextRegistry>();
        registry.Setup(x => x.Registrations).Returns([CreateContextRegistration()]);
        registry.Setup(x => x.GetRegistration(typeof(TestContext))).Returns(CreateContextRegistration());
        return registry.Object;
    }

    private static IQueryViewRegistry CreateViewRegistry()
    {
        var registry = new Mock<IQueryViewRegistry>();
        registry.Setup(x => x.Registrations).Returns([CreateViewRegistration()]);
        return registry.Object;
    }

    private static IDelegatedQueryViewRegistry CreateDelegatedViewRegistry()
    {
        var registry = new Mock<IDelegatedQueryViewRegistry>();
        registry.Setup(x => x.Registrations).Returns(Array.Empty<DelegatedQueryViewRegistration>());
        return registry.Object;
    }

    private static IQueryableRegistry CreateQueryableRegistry()
    {
        var registry = new Mock<IQueryableRegistry>();
        registry.Setup(x => x.Registrations).Returns([
            new QueryableContextRegistryItem
            {
                Name = "Test-Context",
                Description = "Test Context",
                DisplayName = "Test Context",
                Version = "1.0.0",
                Source = "Unit Test",
                Kind = QueryContextKind.Direct,
                Views = [
                    new QueryableViewRegistryItem
                    {
                        Name = "Test-View",
                        Description = "Test View",
                        DisplayName = "Test View",
                        Version = "1.0.0",
                        Visibility = QueryViewVisibility.Public
                    }
                ]
            }
        ]);
        registry.Setup(x => x.GetRegistration("Test-Context")).Returns(
            new QueryableContextRegistryItem
            {
                Name = "Test-Context",
                Description = "Test Context",
                DisplayName = "Test Context",
                Version = "1.0.0",
                Source = "Unit Test",
                Kind = QueryContextKind.Direct,
                Views = [
                    new QueryableViewRegistryItem
                    {
                        Name = "Test-View",
                        Description = "Test View",
                        DisplayName = "Test View",
                        Version = "1.0.0",
                        Visibility = QueryViewVisibility.Public
                    }
                ]
            });
        return registry.Object;
    }

    private static QueryContextRegistration CreateContextRegistration() =>
        new(
            typeof(TestContext),
            typeof(TestSource),
            new QueryContextMetadata(
                "Test-Context",
                "Test Context",
                "Test Context",
                "1.0.0",
                "Unit Test",
                QueryContextKind.Direct,
                null,
                []));

    private static QueryViewRegistration CreateViewRegistration() =>
        new(
            typeof(TestView),
            typeof(TestViewContract),
            typeof(EmptyQueryViewParameters),
            typeof(TestContext),
            new QueryViewMetadata(
                "Test-View",
                "1.0.0",
                "Test View",
                "Test View",
                QueryViewVisibility.Public,
                null,
                [],
                []));

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
