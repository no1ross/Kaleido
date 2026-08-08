//using Kaleido;
//using Kaleido.Queryable.AspNetCore.Contracts;
//using Kaleido.Queryable.AspNetCore.OpenApi;
//using Kaleido.Queryable.FunctionalTests.Fixtures;
//using Kaleido.Queryable.Metadata;
//using Kaleido.Queryable.OpenApi;
//using Kaleido.Queryable.Records;
//using Kaleido.Queryable.Shared;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Options;
//using Microsoft.OpenApi;
//using System.Text.Json.Nodes;
//using Xunit.Abstractions;

//namespace Kaleido.Queryable.AspNetCore.Tests.OpenApi;

//public sealed class AspNetCoreOpenApiDocumentContributorTests
//    : IClassFixture<QueryableFixture>
//{
//    private const string ApplicationJson = "application/json";

//    private readonly QueryableFixture _fixture;
//    private readonly ITestOutputHelper _output;

//    public AspNetCoreOpenApiDocumentContributorTests(
//        QueryableFixture fixture,
//        ITestOutputHelper output)
//    {
//        _fixture = fixture;
//        _output = output;
//    }

//    //[Fact]
//    //public void Contribute_Should_Enrich_Record_Schemas()
//    //{
//    //    using var scope =
//    //        _fixture.CreateScope();

//    //    var registry =
//    //        scope.ServiceProvider
//    //            .GetRequiredService<IRecordRegistry>();

//    //    var document =
//    //        new OpenApiDocument
//    //        {
//    //            Components =
//    //                new OpenApiComponents
//    //                {
//    //                    Schemas =
//    //                        new Dictionary<string, IOpenApiSchema>(
//    //                            StringComparer.Ordinal)
//    //                }
//    //        };

//    //    foreach (var registration in registry.Registrations)
//    //    {
//    //        document.Components.Schemas.Add(
//    //            registration.RecordType.Name,
//    //            OpenApiSchemaFactory.CreateRecordSchema(
//    //                registration));
//    //    }

//    //    var contributor =
//    //        new KaleidoDocumentFilter(
//    //            registry, new QueryableRouteOptions());

//    //    contributor.Apply(document, null);

//    //    var schema =
//    //        document.Components.Schemas[
//    //            nameof(SampleKaleidoRecord)];

//    //    Assert.NotNull(schema);

//    //    // BREAKPOINT HERE
//    //}

//    //[Fact]
//    public void Contribute_Should_Enrich_Queryable_OpenApi_Metadata()
//    {
//        using var scope =
//            _fixture.CreateScope();

//        var registry =
//            scope.ServiceProvider
//                .GetRequiredService<IRecordRegistry>();

//        var routeOptions =
//            GetRouteOptions(
//                scope.ServiceProvider);

//        var document =
//            CreateSwaggerLikeDocument(
//                registry,
//                routeOptions);

//        //var contributor =
//        //    new AspNetCoreOpenApiDocumentContributor(
//        //        registry,
//        //        routeOptions);
//        var contributor =
//            new AspNetCoreOpenApiDocumentContributor(
//                registry, routeOptions);

//        contributor.Contribute(
//            document);

//        var json =
//            SerializeDocument(
//                document);

//        _output.WriteLine(json);

//        Assert.NotNull(
//            document.Components);

//        Assert.NotNull(
//            document.Components.Schemas);

//        Assert.NotEmpty(
//            document.Components.Schemas);

//        Assert.NotEmpty(
//            document.Paths);
//    }

//    private static QueryableRouteOptions GetRouteOptions(
//        IServiceProvider serviceProvider)
//    {
//        var options =
//            serviceProvider
//                .GetService<IOptions<QueryableRouteOptions>>();

//        return options?.Value
//               ?? new QueryableRouteOptions();
//    }

//    private static OpenApiDocument CreateSwaggerLikeDocument(
//        IRecordRegistry registry,
//        QueryableRouteOptions routeOptions)
//    {
//        var document =
//            new OpenApiDocument
//            {
//                Info =
//                    new OpenApiInfo
//                    {
//                        Title = "Kaleido Queryable Test",
//                        Version = "1.0"
//                    },
//                Components =
//                    new OpenApiComponents
//                    {
//                        Schemas =
//                            new Dictionary<string, IOpenApiSchema>(
//                                StringComparer.Ordinal)
//                    }
//            };

//        AddCatalogPath(
//            document,
//            routeOptions);

//        foreach (var registration in registry.Registrations)
//        {
//            AddRecordSchema(
//                document,
//                registration);

//            AddRecordPaths(
//                document,
//                registration,
//                routeOptions);
//        }

//        return document;
//    }

//    private static void AddCatalogPath(
//        OpenApiDocument document,
//        QueryableRouteOptions routeOptions)
//    {
//        var operation =
//            new OpenApiOperation
//            {
//                OperationId = "get_records",
//                Summary = "Gets available records.",
//                Responses =
//                    CreateJsonResponse(
//                        "Available records.",
//                        new OpenApiSchema
//                        {
//                            Type = JsonSchemaType.Array,
//                            Items =
//                                new OpenApiSchemaReference(
//                                    "RecordSummaryContract",
//                                    document)
//                        })
//            };

//        AddOperation(
//            document,
//            routeOptions.RoutePrefix,
//            HttpMethod.Get,
//            operation);
//    }

//    private static void AddRecordPaths(
//        OpenApiDocument document,
//        RecordRegistration registration,
//        QueryableRouteOptions routeOptions)
//    {
//        var recordName =
//            registration.Metadata.Name
//                .ToLowerInvariant();

//        AddRecordMetadataPath(
//            document,
//            registration,
//            routeOptions,
//            recordName);

//        AddRecordQueryPath(
//            document,
//            registration,
//            routeOptions,
//            recordName);

//        foreach (var namedQuery in registration.NamedQueryTypes)
//        {
//            var queryName =
//                namedQuery.Metadata.Name
//                    .ToLowerInvariant();

//            AddNamedQueryPath(
//                document,
//                registration,
//                namedQuery,
//                routeOptions,
//                recordName,
//                queryName);

//            AddNamedQueryMetadataPath(
//                document,
//                registration,
//                namedQuery,
//                routeOptions,
//                recordName,
//                queryName);
//        }
//    }

//    private static void AddRecordMetadataPath(
//        OpenApiDocument document,
//        RecordRegistration registration,
//        QueryableRouteOptions routeOptions,
//        string recordName)
//    {
//        var operation =
//            new OpenApiOperation
//            {
//                OperationId =
//                    QueryableEndpointNames.RecordMetadataEndpointName(
//                        recordName),
//                Summary =
//                    $"Gets metadata for {registration.Metadata.Name}.",
//                Responses =
//                    CreateJsonResponse(
//                        "The record metadata.",
//                        new OpenApiSchemaReference(
//                            "RecordContract",
//                            document))
//            };

//        AddOperation(
//            document,
//            QueryableRoutePaths.RecordMetadata(
//                routeOptions,
//                recordName),
//            HttpMethod.Get,
//            operation);
//    }

//    private static void AddRecordQueryPath(
//        OpenApiDocument document,
//        RecordRegistration registration,
//        QueryableRouteOptions routeOptions,
//        string recordName)
//    {
//        var operation =
//            new OpenApiOperation
//            {
//                OperationId =
//                    QueryableEndpointNames.RecordQueryEndpointName(
//                        recordName),
//                Summary =
//                    $"Queries {registration.Metadata.Name}.",
//                RequestBody =
//                    CreateJsonRequestBody(
//                        new OpenApiSchemaReference(
//                            "QueryApiRequest",
//                            document)),
//                Responses =
//                    CreateJsonResponse(
//                        "The query result.",
//                        new OpenApiSchemaReference(
//                            $"{registration.RecordType.Name}QueryResult",
//                            document))
//            };

//        AddOperation(
//            document,
//            QueryableRoutePaths.RecordQuery(
//                routeOptions,
//                recordName),
//            HttpMethod.Post,
//            operation);
//    }

//    private static void AddNamedQueryPath(
//        OpenApiDocument document,
//        RecordRegistration registration,
//        NamedQueryRegistration namedQuery,
//        QueryableRouteOptions routeOptions,
//        string recordName,
//        string queryName)
//    {
//        var operation =
//            new OpenApiOperation
//            {
//                OperationId =
//                    QueryableEndpointNames.NamedQueryEndpointName(
//                        recordName,
//                        queryName),
//                Summary =
//                    namedQuery.Metadata.Name,
//                RequestBody =
//                    CreateJsonRequestBody(
//                        new OpenApiSchemaReference(
//                            "NamedQueryApiRequest",
//                            document)),
//                Responses =
//                    CreateJsonResponse(
//                        "The named query result.",
//                        new OpenApiSchemaReference(
//                            $"{registration.RecordType.Name}QueryResult",
//                            document))
//            };

//        AddOperation(
//            document,
//            QueryableRoutePaths.NamedQuery(
//                routeOptions,
//                recordName,
//                queryName),
//            HttpMethod.Post,
//            operation);
//    }

//    private static void AddNamedQueryMetadataPath(
//        OpenApiDocument document,
//        RecordRegistration registration,
//        NamedQueryRegistration namedQuery,
//        QueryableRouteOptions routeOptions,
//        string recordName,
//        string queryName)
//    {
//        var operation =
//            new OpenApiOperation
//            {
//                OperationId =
//                    QueryableEndpointNames.NamedQueryMetadataEndpointName(
//                        recordName,
//                        queryName),
//                Summary =
//                    $"Gets metadata for named query '{namedQuery.Metadata.Name}'.",
//                Responses =
//                    CreateJsonResponse(
//                        "The named query metadata.",
//                        new OpenApiSchemaReference(
//                            "NamedQueryContract",
//                            document))
//            };

//        AddOperation(
//            document,
//            QueryableRoutePaths.NamedQueryMetadata(
//                routeOptions,
//                recordName,
//                queryName),
//            HttpMethod.Get,
//            operation);
//    }

//    private static void AddRecordSchema(
//        OpenApiDocument document,
//        RecordRegistration registration)
//    {
//        document.Components ??=
//            new OpenApiComponents();

//        document.Components.Schemas ??=
//            new Dictionary<string, IOpenApiSchema>(
//                StringComparer.Ordinal);

//        document.Components.Schemas[
//            registration.RecordType.Name] =
//            CreateSwaggerLikeRecordSchema(
//                registration);

//        document.Components.Schemas[
//            $"{registration.RecordType.Name}QueryResult"] =
//            CreateSwaggerLikeQueryResultSchema(
//                document,
//                registration);
//    }

//    private static OpenApiSchema CreateSwaggerLikeRecordSchema(
//        RecordRegistration registration)
//    {
//        var schema =
//            new OpenApiSchema
//            {
//                Type = JsonSchemaType.Object,
//                Properties =
//                    new Dictionary<string, IOpenApiSchema>(
//                        StringComparer.Ordinal)
//            };

//        foreach (var field in registration.Metadata.Fields)
//        {
//            schema.Properties[field.Name] =
//                CreateSwaggerLikePropertySchema(
//                    field.FieldType);
//        }

//        return schema;
//    }

//    private static OpenApiSchema CreateSwaggerLikeQueryResultSchema(
//        OpenApiDocument document,
//        RecordRegistration registration)
//    {
//        return new OpenApiSchema
//        {
//            Type = JsonSchemaType.Object,
//            Properties =
//                new Dictionary<string, IOpenApiSchema>(
//                    StringComparer.Ordinal)
//                {
//                    ["totalCount"] =
//                        new OpenApiSchema
//                        {
//                            Type = JsonSchemaType.Integer,
//                            Format = "int32"
//                        },

//                    ["offset"] =
//                        new OpenApiSchema
//                        {
//                            Type = JsonSchemaType.Integer,
//                            Format = "int32"
//                        },

//                    ["pageSize"] =
//                        new OpenApiSchema
//                        {
//                            Type = JsonSchemaType.Integer,
//                            Format = "int32"
//                        },

//                    ["records"] =
//                        new OpenApiSchema
//                        {
//                            Type = JsonSchemaType.Array,
//                            Items =
//                                new OpenApiSchemaReference(
//                                    registration.RecordType.Name,
//                                    document)
//                        }
//                }
//        };
//    }

//    private static OpenApiSchema CreateSwaggerLikePropertySchema(
//        Type type)
//    {
//        var actualType =
//            Nullable.GetUnderlyingType(type)
//            ?? type;

//        if (actualType.IsEnum)
//        {
//            return CreateSwaggerLikeEnumSchema(
//                actualType);
//        }

//        var descriptor =
//            DataTypeMapper.GetDescriptor(
//                type);

//        var schema =
//            new OpenApiSchema
//            {
//                Type =
//                    MapJsonSchemaType(
//                        descriptor.Type),
//                Format =
//                    descriptor.Format
//            };

//        if (descriptor.Nullable)
//        {
//            schema.Type |=
//                JsonSchemaType.Null;
//        }

//        if (descriptor.ItemType is not null)
//        {
//            schema.Items =
//                new OpenApiSchema
//                {
//                    Type =
//                        MapJsonSchemaType(
//                            descriptor.ItemType.Type),
//                    Format =
//                        descriptor.ItemType.Format
//                };
//        }

//        return schema;
//    }

//    private static OpenApiSchema CreateSwaggerLikeEnumSchema(
//        Type enumType)
//    {
//        var names =
//            Enum.GetNames(
//                enumType);

//        return new OpenApiSchema
//        {
//            Type = JsonSchemaType.Integer,
//            Format = "int32",
//            Enum =
//                names
//                    .Select((_, index) =>
//                        JsonValue.Create(index)!)
//                    .Cast<JsonNode>()
//                    .ToList()
//        };
//    }

//    private static OpenApiRequestBody CreateJsonRequestBody(
//        IOpenApiSchema schema)
//    {
//        return new OpenApiRequestBody
//        {
//            Required = true,
//            Content =
//                new Dictionary<string, OpenApiMediaType>(
//                    StringComparer.Ordinal)
//                {
//                    [ApplicationJson] =
//                        new OpenApiMediaType
//                        {
//                            Schema = schema
//                        }
//                }
//        };
//    }

//    private static OpenApiResponses CreateJsonResponse(
//        string description,
//        IOpenApiSchema schema)
//    {
//        return new OpenApiResponses
//        {
//            ["200"] =
//                new OpenApiResponse
//                {
//                    Description = description,
//                    Content =
//                        new Dictionary<string, OpenApiMediaType>(
//                            StringComparer.Ordinal)
//                        {
//                            [ApplicationJson] =
//                                new OpenApiMediaType
//                                {
//                                    Schema = schema
//                                }
//                        }
//                }
//        };
//    }

//    private static void AddOperation(
//        OpenApiDocument document,
//        string route,
//        HttpMethod method,
//        OpenApiOperation operation)
//    {
//        var path =
//            ToOpenApiPath(
//                route);

//        if (!document.Paths.TryGetValue(
//                path,
//                out var existingPathItem) ||
//            existingPathItem is not OpenApiPathItem pathItem)
//        {
//            pathItem =
//                new OpenApiPathItem();

//            document.Paths[path] =
//                pathItem;
//        }

//        pathItem.AddOperation(
//            method,
//            operation);
//    }

//    private static string ToOpenApiPath(
//        string route)
//    {
//        if (string.IsNullOrWhiteSpace(route))
//        {
//            return "/";
//        }

//        return route.StartsWith(
//                "/",
//                StringComparison.Ordinal)
//            ? route
//            : "/" + route;
//    }

//    private static JsonSchemaType MapJsonSchemaType(
//        string type)
//    {
//        return type switch
//        {
//            "string" =>
//                JsonSchemaType.String,

//            "integer" =>
//                JsonSchemaType.Integer,

//            "number" =>
//                JsonSchemaType.Number,

//            "boolean" =>
//                JsonSchemaType.Boolean,

//            "array" =>
//                JsonSchemaType.Array,

//            "object" =>
//                JsonSchemaType.Object,

//            _ =>
//                JsonSchemaType.Object
//        };
//    }

//    private static string SerializeDocument(
//        OpenApiDocument document)
//    {
//        using var writer =
//            new StringWriter();

//        var openApiWriter =
//            new OpenApiJsonWriter(
//                writer);

//        document.SerializeAsV31(
//            openApiWriter);

//        return writer.ToString();
//    }
//}