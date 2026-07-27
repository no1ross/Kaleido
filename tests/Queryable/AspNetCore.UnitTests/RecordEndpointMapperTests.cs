using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text;
using System.Text.Json;

namespace Kaleido.Queryable.AspNetCore.Tests;

public sealed class RecordEndpointMapperTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

    [Fact]
    public void MapRecord_ShouldMap_RecordMetadataEndpoint()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        var endpoint =
            AssertRoute(
                app,
                "functional-records/metadata",
                "GET");

        AssertEndpointName(
            endpoint,
            QueryableEndpointNames.RecordMetadataEndpointName(
                "functional-records"));
    }

    [Fact]
    public void MapRecord_ShouldMap_RecordQueryEndpoint()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        var endpoint =
            AssertRoute(
                app,
                "functional-records/query",
                "POST");

        AssertEndpointName(
            endpoint,
            QueryableEndpointNames.RecordQueryEndpointName(
                "functional-records"));
    }

    [Fact]
    public void MapRecord_ShouldMap_NamedQueryEndpoint()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        var endpoint =
            AssertRoute(
                app,
                "functional-records/queries/records-by-category",
                "POST");

        AssertEndpointName(
            endpoint,
            QueryableEndpointNames.NamedQueryEndpointName(
                "functional-records",
                "records-by-category"));
    }

    [Fact]
    public void MapRecord_ShouldMap_NamedQueryMetadataEndpoint()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        var endpoint =
            AssertRoute(
                app,
                "functional-records/queries/records-by-category/metadata",
                "GET");

        AssertEndpointName(
            endpoint,
            QueryableEndpointNames.NamedQueryMetadataEndpointName(
                "functional-records",
                "records-by-category"));
    }

    [Fact]
    public void MapRecord_ShouldMap_AllNamedQueryEndpoints()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

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

        app.MapRecord(
            registration,
            options);

        AssertRoute(
            app,
            "functional-records/queries/records-by-category",
            "POST");

        AssertRoute(
            app,
            "functional-records/queries/records-by-category/metadata",
            "GET");

        AssertRoute(
            app,
            "functional-records/queries/high-amount-records",
            "POST");

        AssertRoute(
            app,
            "functional-records/queries/high-amount-records/metadata",
            "GET");
    }

    [Fact]
    public void MapRecord_ShouldUse_ConfiguredRouteSegments()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions
            {
                MetadataRoute = "schema",
                QueryRoute = "execute",
                QueriesRoute = "named-queries"
            };

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        AssertRoute(
            app,
            "functional-records/schema",
            "GET");

        AssertRoute(
            app,
            "functional-records/execute",
            "POST");

        AssertRoute(
            app,
            "functional-records/named-queries/records-by-category",
            "POST");

        AssertRoute(
            app,
            "functional-records/named-queries/records-by-category/schema",
            "GET");
    }

    [Fact]
    public void MapRecord_ShouldNormalize_RecordName_ForRoutes()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration(
                recordName: "Functional-Records");

        app.MapRecord(
            registration,
            options);

        AssertRoute(
            app,
            "functional-records/metadata",
            "GET");

        AssertRoute(
            app,
            "functional-records/query",
            "POST");
    }

    [Fact]
    public void MapRecord_ShouldNormalize_NamedQueryName_ForRoutes()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration(
                recordName: "functional-records",
                namedQueries:
                [
                    CreateNamedQueryRegistration(
                        "Records-By-Category")
                ]);

        app.MapRecord(
            registration,
            options);

        AssertRoute(
            app,
            "functional-records/queries/records-by-category",
            "POST");

        AssertRoute(
            app,
            "functional-records/queries/records-by-category/metadata",
            "GET");
    }

    [Fact]
    public async Task MapRecord_ShouldMapMetadataEndpoint_ThatReturnsRecordContract()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        var endpoint =
            AssertEndpointByName(
                app,
                QueryableEndpointNames.RecordMetadataEndpointName(
                    "functional-records"));

        AssertHttpMethod(
            endpoint,
            "GET");

        var response =
            await InvokeEndpointAsync(
                app,
                endpoint);

        Assert.Equal(
            StatusCodes.Status200OK,
            response.StatusCode);

        var contract =
            JsonSerializer.Deserialize<RecordContract>(
                response.Body,
                JsonOptions);

        Assert.NotNull(
            contract);

        Assert.Equal(
            "functional-records",
            contract.Name);

        Assert.Equal(
            "Test record.",
            contract.Description);

        Assert.Equal(
            "1.0.0",
            contract.Version);

        Assert.Equal(
            "Unit Test",
            contract.Source);
    }

    [Fact]
    public async Task MapRecord_ShouldMapNamedQueryMetadataEndpoint_ThatReturnsNamedQueryContract()
    {
        var app =
            CreateApp();

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        var endpoint =
            AssertEndpointByName(
                app,
                QueryableEndpointNames.NamedQueryMetadataEndpointName(
                    "functional-records",
                    "records-by-category"));

        AssertHttpMethod(
            endpoint,
            "GET");

        var response =
            await InvokeEndpointAsync(
                app,
                endpoint);

        Assert.Equal(
            StatusCodes.Status200OK,
            response.StatusCode);

        var contract =
            JsonSerializer.Deserialize<NamedQueryContract>(
                response.Body,
                JsonOptions);

        Assert.NotNull(
            contract);

        Assert.Equal(
            "records-by-category",
            contract.Name);

        Assert.Equal(
            "Test named query.",
            contract.Description);
    }

    [Fact]
    public async Task MapRecord_ShouldMapTypedQueryEndpoint_ThatCallsQueryableCatalog()
    {
        QueryRequest? capturedRequest = null;
        string? capturedRecordKey = null;

        var catalog =
            new Mock<IQueryableCatalog>();

        catalog
            .Setup(x =>
                x.QueryAsync<TestRecord>(
                    It.IsAny<string>(),
                    It.IsAny<QueryRequest>(),
                    It.IsAny<CancellationToken>()))
            .Callback<string, QueryRequest, CancellationToken>(
                (recordKey, request, _) =>
                {
                    capturedRecordKey = recordKey;
                    capturedRequest = request;
                })
            .ReturnsAsync(
                (QueryResult<TestRecord>)null!);

        var app =
            CreateApp(
                catalog.Object);

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        var endpoint =
            AssertEndpointByName(
                app,
                QueryableEndpointNames.RecordQueryEndpointName(
                    "functional-records"));

        AssertHttpMethod(
            endpoint,
            "POST");

        var response =
            await InvokeEndpointAsync(
                app,
                endpoint,
                """
                {
                  "query": {}
                }
                """);

        Assert.Equal(
            StatusCodes.Status200OK,
            response.StatusCode);

        catalog.Verify(
            x => x.QueryAsync<TestRecord>(
                "functional-records",
                It.IsAny<QueryRequest>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(
            "functional-records",
            capturedRecordKey);

        Assert.NotNull(
            capturedRequest);

        Assert.Null(
            capturedRequest.NamedQuery);
    }

    [Fact]
    public async Task MapRecord_ShouldMapTypedNamedQueryEndpoint_ThatCallsQueryableCatalog_WithNamedQueryRequest()
    {
        QueryRequest? capturedRequest = null;
        string? capturedRecordKey = null;

        var catalog =
            new Mock<IQueryableCatalog>();

        catalog
            .Setup(x =>
                x.QueryAsync<TestRecord>(
                    It.IsAny<string>(),
                    It.IsAny<QueryRequest>(),
                    It.IsAny<CancellationToken>()))
            .Callback<string, QueryRequest, CancellationToken>(
                (recordKey, request, _) =>
                {
                    capturedRecordKey = recordKey;
                    capturedRequest = request;
                })
            .ReturnsAsync(
                (QueryResult<TestRecord>)null!);

        var app =
            CreateApp(
                catalog.Object);

        var options =
            new QueryableAspNetCoreOptions();

        var registration =
            CreateRegistration();

        app.MapRecord(
            registration,
            options);

        var endpoint =
            AssertEndpointByName(
                app,
                QueryableEndpointNames.NamedQueryEndpointName(
                    "functional-records",
                    "records-by-category"));

        AssertHttpMethod(
            endpoint,
            "POST");

        var response =
            await InvokeEndpointAsync(
                app,
                endpoint,
                """
                {
                  "values": {
                    "Category": "Medical"
                  }
                }
                """);

        Assert.Equal(
            StatusCodes.Status200OK,
            response.StatusCode);

        catalog.Verify(
            x => x.QueryAsync<TestRecord>(
                "functional-records",
                It.Is<QueryRequest>(request =>
                    request.NamedQuery != null &&
                    request.NamedQuery.Name == "records-by-category"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.Equal(
            "functional-records",
            capturedRecordKey);

        Assert.NotNull(
            capturedRequest);

        Assert.NotNull(
            capturedRequest.NamedQuery);

        Assert.Equal(
            "records-by-category",
            capturedRequest.NamedQuery.Name);

        Assert.NotNull(
            capturedRequest.NamedQuery.Parameters);

        Assert.True(
            capturedRequest.NamedQuery.Parameters.ContainsKey(
                "Category"));
    }

    private static WebApplication CreateApp(
        IQueryableCatalog? catalog = null)
    {
        var builder =
            WebApplication.CreateBuilder();

        builder.Services.AddSingleton(
            catalog ?? Mock.Of<IQueryableCatalog>());

        return builder.Build();
    }

    private static async Task<EndpointInvocationResult> InvokeEndpointAsync(
        WebApplication app,
        RouteEndpoint endpoint,
        string? requestBody = null)
    {
        var context =
            new DefaultHttpContext
            {
                RequestServices = app.Services
            };

        context.SetEndpoint(
            endpoint);

        if (requestBody is not null)
        {
            var requestBytes =
                Encoding.UTF8.GetBytes(
                    requestBody);

            context.Request.Method =
                HttpMethods.Post;

            context.Request.Body =
                new MemoryStream(
                    requestBytes);

            context.Request.ContentType =
                "application/json";

            context.Request.ContentLength =
                requestBytes.Length;

            context.Features.Set<IHttpRequestBodyDetectionFeature>(
                new TestHttpRequestBodyDetectionFeature(
                    canHaveBody: true));
        }
        else
        {
            context.Request.Method =
                HttpMethods.Get;

            context.Features.Set<IHttpRequestBodyDetectionFeature>(
                new TestHttpRequestBodyDetectionFeature(
                    canHaveBody: false));
        }

        await using var responseBody =
            new MemoryStream();

        context.Response.Body =
            responseBody;

        await endpoint.RequestDelegate!(
            context);

        responseBody.Position = 0;

        using var reader =
            new StreamReader(
                responseBody);

        var body =
            await reader.ReadToEndAsync();

        return new EndpointInvocationResult(
            context.Response.StatusCode,
            body);
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

    private sealed record EndpointInvocationResult(
        int StatusCode,
        string Body);

    private sealed class TestHttpRequestBodyDetectionFeature
        : IHttpRequestBodyDetectionFeature
    {
        public TestHttpRequestBodyDetectionFeature(
            bool canHaveBody)
        {
            CanHaveBody = canHaveBody;
        }

        public bool CanHaveBody { get; }
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