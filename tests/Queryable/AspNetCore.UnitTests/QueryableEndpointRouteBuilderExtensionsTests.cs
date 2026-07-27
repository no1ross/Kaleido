using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Kaleido.Queryable.AspNetCore.Tests;

public sealed class QueryableEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public void MapQueryable_ShouldThrow_WhenEndpointsIsNull()
    {
        IEndpointRouteBuilder? endpoints = null;

        Assert.Throws<ArgumentNullException>(
            () => endpoints!.MapQueryable());
    }

    [Fact]
    public void MapQueryable_ShouldThrow_WhenRecordRegistryIsNotRegistered()
    {
        var app =
            CreateAppWithoutRegistry();

        var exception =
            Assert.Throws<InvalidOperationException>(
                () => app.MapQueryable());

        Assert.Contains(
            nameof(IRecordRegistry),
            exception.Message);
    }

    [Fact]
    public void MapQueryable_ShouldReturn_SameEndpointBuilder()
    {
        var registration =
            CreateRegistration();

        var app =
            CreateApp(
                registrations: [registration]);

        var result =
            app.MapQueryable();

        Assert.Same(
            app,
            result);
    }

    [Fact]
    public void MapQueryable_ShouldMap_CatalogEndpoint()
    {
        var registration =
            CreateRegistration();

        var app =
            CreateApp(
                registrations: [registration]);

        app.MapQueryable();

        var endpoint =
            AssertEndpointByName(
                app,
                QueryableEndpointNames.CatalogEndpointName);

        AssertHttpMethod(
            endpoint,
            "GET");
    }

    [Fact]
    public void MapQueryable_ShouldMap_RecordMetadataEndpoint()
    {
        var registration =
            CreateRegistration();

        var app =
            CreateApp(
                registrations: [registration]);

        app.MapQueryable();

        var endpoint =
            AssertRoute(
                app,
                "/kaleido/queryable/functional-records/metadata",
                "GET");

        AssertEndpointName(
            endpoint,
            QueryableEndpointNames.RecordMetadataEndpointName(
                "functional-records"));
    }

    [Fact]
    public void MapQueryable_ShouldMap_RecordQueryEndpoint()
    {
        var registration =
            CreateRegistration();

        var app =
            CreateApp(
                registrations: [registration]);

        app.MapQueryable();

        var endpoint =
            AssertRoute(
                app,
                "/kaleido/queryable/functional-records/query",
                "POST");

        AssertEndpointName(
            endpoint,
            QueryableEndpointNames.RecordQueryEndpointName(
                "functional-records"));
    }

    [Fact]
    public void MapQueryable_ShouldMap_NamedQueryEndpoint()
    {
        var registration =
            CreateRegistration();

        var app =
            CreateApp(
                registrations: [registration]);

        app.MapQueryable();

        var endpoint =
            AssertRoute(
                app,
                "/kaleido/queryable/functional-records/queries/records-by-category",
                "POST");

        AssertEndpointName(
            endpoint,
            QueryableEndpointNames.NamedQueryEndpointName(
                "functional-records",
                "records-by-category"));
    }

    [Fact]
    public void MapQueryable_ShouldMap_NamedQueryMetadataEndpoint()
    {
        var registration =
            CreateRegistration();

        var app =
            CreateApp(
                registrations: [registration]);

        app.MapQueryable();

        var endpoint =
            AssertRoute(
                app,
                "/kaleido/queryable/functional-records/queries/records-by-category/metadata",
                "GET");

        AssertEndpointName(
            endpoint,
            QueryableEndpointNames.NamedQueryMetadataEndpointName(
                "functional-records",
                "records-by-category"));
    }

    [Fact]
    public void MapQueryable_ShouldMap_AllRegisteredRecords()
    {
        var firstRegistration =
            CreateRegistration(
                recordName: "functional-records");

        var secondRegistration =
            CreateRegistration(
                recordName: "other-records");

        var app =
            CreateApp(
                registrations:
                [
                    firstRegistration,
                    secondRegistration
                ]);

        app.MapQueryable();

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/metadata",
            "GET");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/query",
            "POST");

        AssertRoute(
            app,
            "/kaleido/queryable/other-records/metadata",
            "GET");

        AssertRoute(
            app,
            "/kaleido/queryable/other-records/query",
            "POST");
    }

    [Fact]
    public void MapQueryable_ShouldMap_AllNamedQueries_ForRegisteredRecord()
    {
        var registration =
            CreateRegistration(
                recordName: "functional-records",
                namedQueries:
                [
                    CreateNamedQueryRegistration(
                        "records-by-category"),

                    CreateNamedQueryRegistration(
                        "high-amount-records")
                ]);

        var app =
            CreateApp(
                registrations: [registration]);

        app.MapQueryable();

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/queries/records-by-category",
            "POST");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/queries/records-by-category/metadata",
            "GET");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/queries/high-amount-records",
            "POST");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/queries/high-amount-records/metadata",
            "GET");
    }

    [Fact]
    public void MapQueryable_ShouldUse_ConfiguredRoutePrefix()
    {
        var registration =
            CreateRegistration();

        var app =
            CreateApp(
                configure: options =>
                {
                    options.RoutePrefix = "/custom/queryable";
                },
                registrations: [registration]);

        app.MapQueryable();

        var catalogEndpoint =
            AssertEndpointByName(
                app,
                QueryableEndpointNames.CatalogEndpointName);

        AssertHttpMethod(
            catalogEndpoint,
            "GET");

        AssertRoute(
            app,
            "/custom/queryable/functional-records/metadata",
            "GET");

        AssertRoute(
            app,
            "/custom/queryable/functional-records/query",
            "POST");

        AssertRoute(
            app,
            "/custom/queryable/functional-records/queries/records-by-category",
            "POST");

        AssertRoute(
            app,
            "/custom/queryable/functional-records/queries/records-by-category/metadata",
            "GET");
    }

    [Fact]
    public void MapQueryable_ShouldUse_ConfiguredRouteSegments()
    {
        var registration =
            CreateRegistration();

        var app =
            CreateApp(
                configure: options =>
                {
                    options.MetadataRoute = "schema";
                    options.QueryRoute = "execute";
                    options.QueriesRoute = "named-queries";
                },
                registrations: [registration]);

        app.MapQueryable();

        var catalogEndpoint =
            AssertEndpointByName(
                app,
                QueryableEndpointNames.CatalogEndpointName);

        AssertHttpMethod(
            catalogEndpoint,
            "GET");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/schema",
            "GET");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/execute",
            "POST");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/named-queries/records-by-category",
            "POST");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/named-queries/records-by-category/schema",
            "GET");
    }

    [Fact]
    public void MapQueryable_ShouldNormalize_RecordNames_ForRoutes()
    {
        var registration =
            CreateRegistration(
                recordName: "Functional-Records");

        var app =
            CreateApp(
                registrations: [registration]);

        app.MapQueryable();

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/metadata",
            "GET");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/query",
            "POST");
    }

    [Fact]
    public void MapQueryable_ShouldNormalize_NamedQueryNames_ForRoutes()
    {
        var registration =
            CreateRegistration(
                recordName: "functional-records",
                namedQueries:
                [
                    CreateNamedQueryRegistration(
                        "Records-By-Category")
                ]);

        var app =
            CreateApp(
                registrations: [registration]);

        app.MapQueryable();

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/queries/records-by-category",
            "POST");

        AssertRoute(
            app,
            "/kaleido/queryable/functional-records/queries/records-by-category/metadata",
            "GET");
    }

    private static WebApplication CreateApp(
        RecordRegistration[] registrations,
        Action<QueryableAspNetCoreOptions>? configure = null)
    {
        var builder =
            WebApplication.CreateBuilder();

        var registry =
            new Mock<IRecordRegistry>();

        registry
            .SetupGet(x => x.Registrations)
            .Returns(registrations);

        builder.Services.AddSingleton(
            registry.Object);

        builder.Services.AddSingleton(
            Mock.Of<IQueryableService>());

        builder.Services.AddSingleton(
            Options.Create(
                CreateOptions(configure)));

        return builder.Build();
    }

    private static WebApplication CreateAppWithoutRegistry()
    {
        var builder =
            WebApplication.CreateBuilder();

        builder.Services.AddSingleton(
            Options.Create(
                new QueryableAspNetCoreOptions()));

        builder.Services.AddSingleton(
            Mock.Of<IQueryableService>());

        return builder.Build();
    }

    private static QueryableAspNetCoreOptions CreateOptions(
        Action<QueryableAspNetCoreOptions>? configure)
    {
        var options =
            new QueryableAspNetCoreOptions();

        configure?.Invoke(options);

        return options;
    }

    private static RouteEndpoint AssertRoute(
        IEndpointRouteBuilder endpoints,
        string route,
        string httpMethod)
    {
        var normalizedRoute =
            NormalizeRoute(route);

        var matchingEndpoints =
            GetRouteEndpoints(endpoints)
                .Where(endpoint =>
                    string.Equals(
                        NormalizeRoute(endpoint.RoutePattern.RawText),
                        normalizedRoute,
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();

        var endpoint =
            Assert.Single(
                matchingEndpoints);

        AssertHttpMethod(
            endpoint,
            httpMethod);

        return endpoint;
    }

    private static RouteEndpoint AssertEndpointByName(
        IEndpointRouteBuilder endpoints,
        string endpointName)
    {
        var matchingEndpoints =
            GetRouteEndpoints(endpoints)
                .Where(endpoint =>
                    string.Equals(
                        endpoint.Metadata
                            .GetMetadata<IEndpointNameMetadata>()
                            ?.EndpointName,
                        endpointName,
                        StringComparison.Ordinal))
                .ToArray();

        return Assert.Single(
            matchingEndpoints);
    }

    private static void AssertEndpointName(
        RouteEndpoint endpoint,
        string expectedName)
    {
        var endpointNameMetadata =
            endpoint.Metadata
                .GetMetadata<IEndpointNameMetadata>();

        Assert.NotNull(
            endpointNameMetadata);

        Assert.Equal(
            expectedName,
            endpointNameMetadata.EndpointName);
    }

    private static void AssertHttpMethod(
        RouteEndpoint endpoint,
        string expectedHttpMethod)
    {
        var httpMethodMetadata =
            endpoint.Metadata
                .GetMetadata<HttpMethodMetadata>();

        Assert.NotNull(
            httpMethodMetadata);

        Assert.Contains(
            expectedHttpMethod,
            httpMethodMetadata.HttpMethods);
    }

    private static IReadOnlyCollection<RouteEndpoint> GetRouteEndpoints(
        IEndpointRouteBuilder endpoints)
    {
        return endpoints
            .DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
    }

    private static string NormalizeRoute(
        string? route)
    {
        return (route ?? string.Empty)
            .Trim()
            .Trim('/');
    }

    private static RecordRegistration CreateRegistration(
        string recordName = "functional-records",
        IReadOnlyCollection<NamedQueryRegistration>? namedQueries = null)
    {
        return new RecordRegistration(
            typeof(TestRecord),
            typeof(TestRecordSource),
            new RecordMetadata(
                recordName,
                "Test record.",
                "1.0.0",
                "Unit Test",
                Array.Empty<FieldMetadata>(),
                null),
            namedQueries ??
            [
                CreateNamedQueryRegistration(
                    "records-by-category")
            ]);
    }

    private static NamedQueryRegistration CreateNamedQueryRegistration(
        string name)
    {
        return new NamedQueryRegistration(
            typeof(TestNamedQuery),
            new NamedQueryMetadata(
                name,
                "Test named query.",
                Array.Empty<QueryParameterMetadata>()));
    }

    private sealed class TestRecord
    {
    }

    private sealed class TestRecordSource
    {
    }

    private sealed class TestNamedQuery
    {
    }
}