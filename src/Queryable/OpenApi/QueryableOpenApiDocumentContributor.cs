using Kaleido.Queryable.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.OpenApi;
using Kaleido.Queryable.Records;

namespace Kaleido.Queryable.OpenApi;

public interface IOpenApiDocumentContributor
{
    void Contribute(
        OpenApiDocument document,
        IRecordRegistry recordRegistry);
}

public sealed class QueryableOpenApiDocumentContributor
    : IOpenApiDocumentContributor
{
    public void Contribute(OpenApiDocument document, IRecordRegistry recordRegistry)
    {
        ArgumentNullException.ThrowIfNull(document); 
        ArgumentNullException.ThrowIfNull(recordRegistry); 
        
        foreach (var record in recordRegistry.Registrations) 
        { 
            document.Paths ??= []; 
            
            document.Paths.Add($"/{record.Metadata.Name.ToLowerInvariant()}/query", new OpenApiPathItem());
        }

        foreach (var record in recordRegistry.Registrations)
        {
            AddRecordEndpoints(document, record.Metadata);
            AddQueryEndpoint(document, record.Metadata);
            AddNamedQueryEndpoints(document, record.Metadata);
        }
    }

    private void AddRecordEndpoints(OpenApiDocument document, RecordMetadata record)
    {
    }

    private void AddQueryEndpoint(OpenApiDocument document, RecordMetadata record)
    {
    }

    private void AddNamedQueryEndpoints(OpenApiDocument document, RecordMetadata record)
    {
    }
}
