using Kaleido.Queryable.AspNetCore.Contracts;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.OpenApi;
using Kaleido.Queryable.Records;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace Kaleido.Queryable.AspNetCore.OpenApi;

public sealed class AspNetCoreOpenApiDocumentContributor
{
    private readonly IRecordRegistry _registry;
    private readonly QueryableRouteOptions _routeOptions;

    public AspNetCoreOpenApiDocumentContributor(
        IRecordRegistry registry,
        QueryableRouteOptions routeOptions)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(routeOptions);

        _registry = registry;
        _routeOptions = routeOptions;
    }

    public void Contribute(
        OpenApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        EnrichRecordSchemas(document);

        EnrichNamedQuerySchemas(document);

        EnrichRecordOperations(document);

        EnrichNamedQueryOperations(document);
    }

    private void EnrichRecordSchemas(
        OpenApiDocument document)
    {
        if (document.Components?.Schemas is null)
        {
            return;
        }

        foreach (var registration in _registry.Registrations)
        {
            if (!document.Components.Schemas.TryGetValue(
                    registration.RecordType.Name,
                    out var schema))
            {
                continue;
            }

            schema.Description =
                registration.Metadata.Description;

            if (schema.Properties is null)
            {
                continue;
            }

            foreach (var field in registration.Metadata.Fields)
            {
                var property =
                    FindProperty(
                        schema,
                        field.Name);

                if (property is null)
                {
                    continue;
                }

                property.Description =
                    field.Description;

                EnrichEnumMetadata(
                    property,
                    field.FieldType);
            }
        }
    }

    private void EnrichNamedQuerySchemas(
        OpenApiDocument document)
    {
        if (document.Components is null)
        {
            return;
        }

        document.Components.Schemas ??=
            new Dictionary<string, IOpenApiSchema>(
                StringComparer.Ordinal);

        foreach (var registration in _registry.Registrations)
        {
            foreach (var namedQuery in registration.NamedQueryTypes)
            {
                var schemaName =
                    GetNamedQueryParametersSchemaName(
                        registration,
                        namedQuery);

                document.Components.Schemas[schemaName] =
                    OpenApiSchemaFactory.CreateNamedQuerySchema(
                        namedQuery);
            }
        }
    }

    private void EnrichRecordOperations(
        OpenApiDocument document)
    {
        foreach (var registration in _registry.Registrations)
        {
            var recordName =
                registration.Metadata.Name
                    .ToLowerInvariant();

            EnrichOperation(
                document,
                QueryableRoutePaths.RecordMetadata(
                    _routeOptions,
                    recordName),
                HttpMethod.Get,
                registration.Metadata.Name,
                registration.Metadata.Description);

            EnrichOperation(
                document,
                QueryableRoutePaths.RecordQuery(
                    _routeOptions,
                    recordName),
                HttpMethod.Post,
                registration.Metadata.Name,
                registration.Metadata.Description);
        }
    }

    private void EnrichNamedQueryOperations(
        OpenApiDocument document)
    {
        foreach (var registration in _registry.Registrations)
        {
            var recordName =
                registration.Metadata.Name
                    .ToLowerInvariant();

            foreach (var namedQuery in registration.NamedQueryTypes)
            {
                var queryName =
                    namedQuery.Metadata.Name
                        .ToLowerInvariant();

                var metadataOperation =
                    FindOperation(
                        document,
                        QueryableRoutePaths.NamedQueryMetadata(
                            _routeOptions,
                            recordName,
                            queryName),
                        HttpMethod.Get);

                if (metadataOperation is not null)
                {
                    metadataOperation.Summary =
                        namedQuery.Metadata.Name;

                    metadataOperation.Description =
                        namedQuery.Metadata.Description;
                }

                var queryOperation =
                    FindOperation(
                        document,
                        QueryableRoutePaths.NamedQuery(
                            _routeOptions,
                            recordName,
                            queryName),
                        HttpMethod.Post);

                if (queryOperation is null)
                {
                    continue;
                }

                queryOperation.Summary =
                    namedQuery.Metadata.Name;

                queryOperation.Description =
                    namedQuery.Metadata.Description;

                //
                // Replace the generic NamedQueryApiRequest
                // schema with the actual metadata-driven
                // parameter schema.
                //
                if (queryOperation.RequestBody?.Content is not null &&
                    queryOperation.RequestBody.Content.TryGetValue(
                        "application/json",
                        out var mediaType))
                {
                    mediaType.Schema =
                        OpenApiSchemaFactory.CreateNamedQuerySchema(
                            namedQuery);
                }
            }
        }
    }

    private static void EnrichOperation(
        OpenApiDocument document,
        string route,
        HttpMethod method,
        string summary,
        string? description)
    {
        var operation =
            FindOperation(
                document,
                route,
                method);

        if (operation is null)
        {
            return;
        }

        operation.Summary =
            summary;

        operation.Description =
            description;
    }

    private static OpenApiOperation? FindOperation(
        OpenApiDocument document,
        string route,
        HttpMethod method)
    {
        var path =
            ToOpenApiPath(route);

        if (!document.Paths.TryGetValue(
                path,
                out var pathItem))
        {
            return null;
        }

        if (pathItem.Operations is null)
        {
            return null;
        }

        if (!pathItem.Operations.TryGetValue(
                method,
                out var operation))
        {
            return null;
        }

        return operation;
    }

    private static OpenApiSchema? FindProperty(
        IOpenApiSchema schema,
        string propertyName)
    {
        if (schema.Properties is null)
        {
            return null;
        }

        foreach (var property in schema.Properties)
        {
            if (string.Equals(
                    property.Key,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return property.Value as OpenApiSchema;
            }
        }

        return null;
    }

    private static void EnrichEnumMetadata(
        OpenApiSchema schema,
        Type fieldType)
    {
        var descriptor =
            DataTypeMapper.GetDescriptor(
                fieldType);

        if (descriptor.EnumValues is null ||
            descriptor.EnumValues.Count == 0)
        {
            return;
        }

        schema.Type =
            JsonSchemaType.String;

        schema.Format =
            null;

        schema.Enum ??=
            new List<JsonNode>();

        schema.Enum.Clear();

        foreach (var value in descriptor.EnumValues)
        {
            schema.Enum.Add(
                JsonValue.Create(value)!);
        }
    }

    private static string GetNamedQueryParametersSchemaName(
        RecordRegistration record,
        NamedQueryRegistration namedQuery)
    {
        return
            $"{record.Metadata.Name}.{namedQuery.Metadata.Name}.Parameters";
    }

    private static string ToOpenApiPath(
        string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return "/";
        }

        return route.StartsWith(
                "/",
                StringComparison.Ordinal)
            ? route
            : "/" + route;
    }
}