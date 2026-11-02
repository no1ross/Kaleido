using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Http.Registry;

// The registry's readiness depends on its own store only (#553): registrations must always get through, so
// nothing else - no instance, no other service - may make it unready.
internal sealed class RegistryStoreHealthCheck(IKaleidoRegistryStore store) : IHealthCheck
{
    internal const string Name = "kaleido-registry-store";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await store.ReadAsync(cancellationToken);

        return HealthCheckResult.Healthy("The registry store answers.");
    }
}
