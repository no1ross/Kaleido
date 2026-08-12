using Kaleido.Queryable.AspNetCore;
using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace Kaleido.Queryable.UnitTests.AspNetCore;

public sealed class QueryableEndpointRouteBuilderExtensionsTests
{
    [Fact]
    public void MapQueryable_ShouldThrow_WhenEndpointsIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => QueryableEndpointRouteBuilderExtensions.MapQueryable(null!));
    }

    [Fact]
    public void MapQueryable_ShouldRegisterCatalogEndpoint()
    {
        var endpoints =
            CreateEndpoints(
                CreateRegistry());

        endpoints.MapQueryable();

        var endpoint =
            FindEndpoint(
                endpoints,
                QueryableEndpointNames.CatalogEndpointName);

        Assert.NotNull(endpoint);
    }

    [Fact]
    public void MapQueryable_ShouldRegisterRecordMetadataEndpoint()
    {
        var endpoints =
            CreateEndpoints(
                CreateRegistry());

        endpoints.MapQueryable();

        var endpoint =
            FindEndpoint(
                endpoints,
                QueryableEndpointNames
                    .RecordMetadataEndpointName(
                        "functionalrecords"));

        Assert.NotNull(endpoint);
    }

    [Fact]
    public void MapQueryable_ShouldRegisterRecordQueryEndpoint()
    {
        var endpoints =
            CreateEndpoints(
                CreateRegistry());

        endpoints.MapQueryable();

        var endpoint =
            FindEndpoint(
                endpoints,
                QueryableEndpointNames
                    .RecordQueryEndpointName(
                        "functionalrecords"));

        Assert.NotNull(endpoint);
    }

    [Fact]
    public void MapQueryable_ShouldRegisterNamedQueryMetadataEndpoint()
    {
        var endpoints =
            CreateEndpoints(
                CreateRegistry());

        endpoints.MapQueryable();

        var endpoint =
            FindEndpoint(
                endpoints,
                QueryableEndpointNames
                    .NamedQueryMetadataEndpointName(
                        "functionalrecords",
                        "activerecords"));

        Assert.NotNull(endpoint);
    }

    [Fact]
    public void MapQueryable_ShouldRegisterNamedQueryEndpoint()
    {
        var endpoints =
            CreateEndpoints(
                CreateRegistry());

        endpoints.MapQueryable();

        var endpoint =
            FindEndpoint(
                endpoints,
                QueryableEndpointNames
                    .NamedQueryEndpointName(
                        "functionalrecords",
                        "activerecords"));

        Assert.NotNull(endpoint);
    }

    [Fact]
    public void MapQueryable_ShouldUseDisplayNameForRecordTag()
    {
        var endpoints =
            CreateEndpoints(
                CreateRegistry());

        endpoints.MapQueryable();

        var endpoint =
            FindEndpoint(
                endpoints,
                QueryableEndpointNames
                    .RecordMetadataEndpointName(
                        "functionalrecords"));

        var tags =
            endpoint!.Metadata
                .OfType<ITagsMetadata>()
                .Single();

        Assert.Contains(
            "Functional record test type.",
            tags.Tags);
    }

    [Fact]
    public void MapQueryable_ShouldUseCombinedTagForNamedQuery()
    {
        var endpoints =
            CreateEndpoints(
                CreateRegistry());

        endpoints.MapQueryable();

        var endpoint =
            FindEndpoint(
                endpoints,
                QueryableEndpointNames
                    .NamedQueryEndpointName(
                        "functionalrecords",
                        "activerecords"));

        var tags =
            endpoint!.Metadata
                .OfType<ITagsMetadata>()
                .Single();

        Assert.Contains(
            "Functional record test type. - Returns active records.",
            tags.Tags);
    }

    private static RouteEndpoint? FindEndpoint(
        IEndpointRouteBuilder endpoints,
        string name)
    {
        var dataSource =
            endpoints.DataSources.Single();

        return dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .SingleOrDefault(x =>
                x.Metadata
                    .OfType<IEndpointNameMetadata>()
                    .Any(m =>
                        string.Equals(
                            m.EndpointName,
                            name,
                            StringComparison.Ordinal)));
    }

    private static IEndpointRouteBuilder CreateEndpoints(
        IQueryContextRegistry registry)
    {
        var builder = WebApplication.CreateBuilder();

        var services = builder.Services;

        services.AddRouting();

        services.AddSingleton(
            registry);

        builder.Services.AddSingleton(
            Mock.Of<IQueryableService>());

        services.AddSingleton<
            IOptions<QueryableRouteOptions>>(
            Options.Create(
                new QueryableRouteOptions()));        

        return builder.Build();
    }

    private static IQueryContextRegistry CreateRegistry()
    {
        var namedQuery =
            new NamedQueryRegistration(
                typeof(FakeNamedQuery),
                new NamedQueryMetadata(
                    "ActiveRecords",
                    "Returns active records.",
                    "1.0",
                Array.Empty<QueryParameterMetadata>()));

        var record =
            new QueryRegistration(
                typeof(FakeRecord),
                typeof(FakeSource),
                new QueryMetadata(
                    "FunctionalRecords",
                    "Functional record test type.",
                    "Functional record test type.",
                    "1.0",
                    "Functional Records",
                    Array.Empty<FieldMetadata>(),
                    null),
                new[]
                {
                    namedQuery
                });

        var registry =
            new Mock<IQueryContextRegistry>();

        registry.Setup(x => x.Registrations)
            .Returns(
                new[]
                {
                    record
                });

        return registry.Object;
    }

    private sealed class FakeRecord
    {
    }

    private sealed class FakeNamedQuery
    {
    }
    private sealed class FakeSource
    {
    }
}
