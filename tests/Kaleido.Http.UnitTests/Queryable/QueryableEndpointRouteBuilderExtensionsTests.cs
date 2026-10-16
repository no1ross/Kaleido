using System.Text;
using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Http.Queryable;
using Kaleido.Queryable.Registry;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Queryable.UnitTests;

public sealed class QueryableEndpointRouteBuilderExtensionsTests
    : SutFixture
{
    private static readonly AuthorizationMetadata Radiology = new(null, ["radiology"]);

    private static readonly IReadOnlyCollection<QueryableFieldDescriptor> Fields =
    [
        new QueryableFieldDescriptor
        {
            Name = "Id",
            FieldType = typeof(int),
            DataType = new DataTypeDescriptor("integer"),
            IsFilterable = true,
            FilterOperators = [FilterOperator.Equals]
        }
    ];

    private readonly Mock<IQueryableService> queryable = new();

    [Fact]
    public void MapQueryable_WhenEndpointsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            QueryableEndpointRouteBuilderExtensions.MapQueryable(null!));
    }

    [Fact]
    public void MapQueryable_WhenTheQueryableRuntimeIsNotRegistered_ThrowsAConfigurationError()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = "kaleido" });
        var endpoints = builder.Build();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => endpoints.MapQueryable());

        Assert.Equal(QueryableErrorCodes.InvalidRegistration, exception.Code);
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
    public void MapQueryable_RegistersSourceAndViewEndpoints()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapQueryable();

        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context")));
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

    [Fact]
    public void MapQueryable_WithoutADisplayName_DescribesTheSourceByName()
    {
        var endpoints = CreateEndpoints(sourceDisplayName: null);

        endpoints.MapQueryable();

        var endpoint = FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context"))!;
        Assert.Contains("Test-Context", endpoint.Metadata.GetMetadata<ITagsMetadata>()!.Tags);
        Assert.Equal("Query Test-Context.", endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()!.Summary);
        Assert.Equal(
            "Executes a query against the 'Test-Context' query source.",
            endpoint.Metadata.GetMetadata<IEndpointDescriptionMetadata>()!.Description);
    }

    [Fact]
    public void MapQueryable_WithADisplayName_DescribesTheSourceByDisplayName()
    {
        var endpoints = CreateEndpoints();

        endpoints.MapQueryable();

        var endpoint = FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context"))!;
        Assert.Equal("Query Test Context.", endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>()!.Summary);
    }

    [Fact]
    public void MapQueryable_LogsTheMappedEndpointsOnceAtInformation()
    {
        var logger = new Mock<ILogger>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var endpoints = CreateEndpoints("data", startupLogger: logger);

        endpoints.MapQueryable();

        var log = Assert.Single(logger.Invocations, x => string.Equals(x.Method.Name, nameof(ILogger.Log), StringComparison.Ordinal));
        Assert.Equal(LogLevel.Information, log.Arguments[0]);
        Assert.Equal(
            "Queryable mapped at route prefix /data/queryable with 1 query sources and 1 views.",
            log.Arguments[2]?.ToString());
    }

    [Fact]
    public void MapQueryable_ZeroTrust_MapsAnExplicitSourceAndSkipsItsUnspecifiedView()
    {
        var endpoints = CreateEndpoints(
            mode: KaleidoAuthorizationMode.ZeroTrust,
            sourceAuthorization: Radiology);

        endpoints.MapQueryable();

        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context")));
        Assert.Null(FindEndpoint(endpoints, QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view")));
    }

    [Fact]
    public void MapQueryable_ZeroTrust_MapsAnExplicitViewOfAnUnspecifiedSource()
    {
        var endpoints = CreateEndpoints(
            mode: KaleidoAuthorizationMode.ZeroTrust,
            viewAuthorization: Radiology);

        endpoints.MapQueryable();

        Assert.Null(FindEndpoint(endpoints, QueryableEndpointNames.QuerySourceEndpointName("test-context")));
        Assert.NotNull(FindEndpoint(endpoints, QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view")));
    }

    [Fact]
    public async Task SourceEndpoint_QueriesTheSourceWithTheResolvedBody_AndAnswersOk()
    {
        queryable
            .Setup(x => x.QueryAsync<TestSource, TestContext>(It.IsAny<IQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<TestContext>(3, 0, 10, [new TestContext()]));
        var endpoints = CreateEndpoints();
        endpoints.MapQueryable();

        var context = await InvokeAsync(
            endpoints,
            QueryableEndpointNames.QuerySourceEndpointName("test-context"),
            """{"query":{"filter":{"condition":{"field":"id","operator":"equals","values":[7]}}}}""");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(3, ReadJson(context).GetProperty("totalCount").GetInt32());
        queryable.Verify(
            x => x.QueryAsync<TestSource, TestContext>(
                It.Is<IQueryRequest>(r => Equals(r.Query!.Filter!.Condition!.Values[0], 7)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ViewEndpoint_QueriesTheViewWithItsParameters_AndAnswersOk()
    {
        queryable
            .Setup(x => x.QueryAsync<TestView, TestViewContract>(It.IsAny<IQueryRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryResult<TestViewContract>(1, 0, 10, [new TestViewContract()]));
        var endpoints = CreateEndpoints();
        endpoints.MapQueryable();

        var context = await InvokeAsync(
            endpoints,
            QueryableEndpointNames.QueryViewEndpointName("test-context", "test-view"),
            """{"parameters":{},"query":{"filter":{"condition":{"field":"id","operator":"equals","values":["8"]}}}}""");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(1, ReadJson(context).GetProperty("totalCount").GetInt32());
        queryable.Verify(
            x => x.QueryAsync<TestView, TestViewContract>(
                It.Is<IQueryRequest>(r => r.ViewParameters is EmptyQueryViewParameters && r.Query!.Filter!.Condition!.Field == "id"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SourceEndpoint_WithAnInvalidBody_IsAnsweredByTheExceptionHandler_WithoutQuerying()
    {
        var handler = new Mock<IKaleidoExceptionHandler>();
        var endpoints = CreateEndpoints(exceptionHandler: handler.Object);
        endpoints.MapQueryable();

        await InvokeAsync(
            endpoints,
            QueryableEndpointNames.QuerySourceEndpointName("test-context"),
            "{not json");

        handler.Verify(
            x => x.HandleAsync(It.IsAny<HttpContext>(), It.Is<BadHttpRequestException>(e => e.StatusCode == StatusCodes.Status400BadRequest), It.IsAny<CancellationToken>()),
            Times.Once);
        queryable.Verify(
            x => x.QueryAsync<TestSource, TestContext>(It.IsAny<IQueryRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task<DefaultHttpContext> InvokeAsync(
        WebApplication endpoints,
        string endpointName,
        string body)
    {
        var endpoint = FindEndpoint(endpoints, endpointName)!;
        var context = new DefaultHttpContext
        {
            RequestServices = endpoints.Services.CreateScope().ServiceProvider,
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();

        await endpoint.RequestDelegate!(context);
        return context;
    }

    private static JsonElement ReadJson(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body).RootElement.Clone();
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

    private WebApplication CreateEndpoints(
        string serviceName = "kaleido",
        KaleidoAuthorizationMode mode = KaleidoAuthorizationMode.None,
        AuthorizationMetadata? sourceAuthorization = null,
        AuthorizationMetadata? viewAuthorization = null,
        string? sourceDisplayName = "Test Context",
        Mock<ILogger>? startupLogger = null,
        IKaleidoExceptionHandler? exceptionHandler = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddRouting();
        builder.Services.AddSingleton(new KaleidoHttpOptions());

        builder.Services.AddSingleton(queryable.Object);
        builder.Services.AddSingleton<IQueryableRegistry>(CreateQueryableRegistry(sourceAuthorization, viewAuthorization, sourceDisplayName));
        builder.Services.AddSingleton(new KaleidoServiceOptions { ServiceName = serviceName, AuthorizationMode = mode });

        if (exceptionHandler is not null)
        {
            builder.Services.AddSingleton(exceptionHandler);
        }

        if (startupLogger is not null)
        {
            var factory = new Mock<ILoggerFactory>();
            factory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(startupLogger.Object);
            builder.Services.AddSingleton(factory.Object);
        }

        return builder.Build();
    }

    private static IQueryableRegistry CreateQueryableRegistry(
        AuthorizationMetadata? sourceAuthorization,
        AuthorizationMetadata? viewAuthorization,
        string? sourceDisplayName)
    {
        var registry = new Mock<IQueryableRegistry>();
        registry.Setup(x => x.Registrations).Returns([
            new QueryableSourceRegistryItem
            {
                SourceType = typeof(TestSource),
                QueryContextType = typeof(TestContext),
                ResultType = typeof(TestContext),
                ParametersType = typeof(EmptyQueryViewParameters),
                Name = "Test-Context",
                Description = "Test Context",
                DisplayName = sourceDisplayName,
                Version = "1.0.0",
                Source = "Unit Test",
                Fields = Fields,
                Authorization = sourceAuthorization ?? AuthorizationMetadata.Unspecified,
                Views = [
                    new QueryableViewRegistryItem
                    {
                        QueryViewType = typeof(TestView),
                        ViewType = typeof(TestViewContract),
                        ViewParametersType = typeof(EmptyQueryViewParameters),
                        Name = "Test-View",
                        Description = "Test View",
                        DisplayName = "Test View",
                        Version = "1.0.0",
                        Authorization = viewAuthorization ?? AuthorizationMetadata.Unspecified
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
