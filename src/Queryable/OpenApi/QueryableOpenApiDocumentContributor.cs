using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Records;
using Microsoft.OpenApi;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Queryable.OpenApi;

public interface IOpenApiDocumentContributor
{
    void Contribute(OpenApiDocument document);
}

public sealed class QueryableOpenApiDocumentContributor
    : IOpenApiDocumentContributor
{
    private readonly IRecordRegistry _registry;

    public QueryableOpenApiDocumentContributor(
        IRecordRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        _registry = registry;
    }

    public void Contribute(
        OpenApiDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        foreach (var registration in _registry.Registrations)
        {
            AddRecordMetadataPath(
                document,
                registration);

            AddQueryPath(
                document,
                registration);

            AddNamedQueryPaths(
                document,
                registration);
        }
    }

    private static void AddRecordMetadataPath(
        OpenApiDocument document,
        RecordRegistration registration)
    {
        var pathItem = new OpenApiPathItem();

        pathItem.AddOperation(
            HttpMethod.Get,
            new OpenApiOperation
            {
                Summary = $"Gets metadata for {registration.Metadata.Name}",
                Description = registration.Metadata.Description
            });

        document.Paths.Add(
            $"/queryable/{registration.Metadata.Name}",
            pathItem);
    }

    private static void AddQueryPath(
        OpenApiDocument document,
        RecordRegistration registration)
    {
        var pathItem = new OpenApiPathItem();

        pathItem.AddOperation(
            HttpMethod.Post,
            new OpenApiOperation
            {
                Summary = $"Queries {registration.Metadata.Name}",
                Description = registration.Metadata.Description
            });

        document.Paths.Add(
            $"/queryable/{registration.Metadata.Name}/query",
            pathItem);
    }

    private static void AddNamedQueryPaths(
        OpenApiDocument document,
        RecordRegistration registration)
    {
        foreach (var namedQuery in registration.NamedQueryTypes)
        {
            var pathItem = new OpenApiPathItem();

            pathItem.AddOperation(
                HttpMethod.Post,
                new OpenApiOperation
                {
                    Summary = namedQuery.Metadata.Name,
                    Description = namedQuery.Metadata.Description
                });

            document.Paths.Add(
                $"/queryable/{registration.Metadata.Name}/queries/{namedQuery.Metadata.Name}",
                pathItem);
        }
    }

    private static OpenApiPathItem CreateGetPath(
    string summary,
    string? description = null)
    {
        return new OpenApiPathItem
        {
            // fill this in once we confirm OpenApiPathItem
        };
    }

    private static OpenApiPathItem CreatePostPath(
    string summary,
    string? description = null)
    {
        return new OpenApiPathItem
        {
            // fill this in once we confirm OpenApiPathItem
        };
    }

}