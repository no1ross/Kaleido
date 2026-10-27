using Kaleido.Guide.Examples.Persistence;
using Kaleido.Observability.OpenTelemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Guide.Examples.Deployment;

// A host's operational wiring: health checks and telemetry export.
public static class Operations
{
    public static void AddOperations(IServiceCollection services, IKaleidoBuilder kaleido)
    {
        // snippet: deployment-health
        // Downstream Kaleido services are checked by AddHttpClients() (kaleido-{name}); your database
        // is yours to check.
        services.AddHealthChecks()
            .AddDbContextCheck<OrdersDbContext>("orders-database");
        // end snippet

        // snippet: deployment-telemetry
        kaleido.AddOpenTelemetry();   // export target under Kaleido:OpenTelemetry in configuration
        // end snippet
    }

    // Several MCP instances behind a load balancer: one Data Protection key ring for all of them.
    public static void AddSharedKeyRing(IServiceCollection services, DirectoryInfo sharedKeys)
    {
        // snippet: deployment-key-ring
        // Every instance: the same application name and the same key storage. A folder every instance
        // mounts works as is; packages store the keys in Redis, Azure Blob Storage or a database
        // (PersistKeysToStackExchangeRedis, PersistKeysToAzureBlobStorage, PersistKeysToDbContext).
        services.AddDataProtection()
            .SetApplicationName("orders")
            .PersistKeysToFileSystem(sharedKeys);
        // end snippet
    }

    public static void MapOperations(WebApplication app)
    {
        // snippet: deployment-health-map
        app.MapHealthChecks("/health");
        // end snippet
    }
}
