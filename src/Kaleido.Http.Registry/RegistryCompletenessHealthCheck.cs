using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Http.Registry;

// The registry's completeness (#553), separate from its readiness: every expected processor version and
// every service a published step looks up is registered, and every version has a ready instance. Built from
// the store only - the registry never calls an instance.
internal sealed class RegistryCompletenessHealthCheck(IKaleidoRegistrySource registrations) : IHealthCheck
{
    internal const string Name = "kaleido-registry-completeness";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var view = await registrations.GetAsync(maxParallelism: null, cancellationToken);
        var unavailable = view.Services.Where(x => !x.Available).Select(x => $"{x.ServiceName}/{x.ServiceVersion}").ToList();
        var missing = view.Missing.Select(x => x.ServiceVersion is null ? x.ServiceName : $"{x.ServiceName}/{x.ServiceVersion}").ToList();
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["missing"] = missing,
            ["unavailable"] = unavailable,
        };

        return missing.Count == 0 && unavailable.Count == 0
            ? HealthCheckResult.Healthy("Every expected service version is registered and has a ready instance.", data)
            : HealthCheckResult.Degraded($"Missing: {Join(missing)}; no ready instance: {Join(unavailable)}.", data: data);
    }

    private static string Join(List<string> names) => names.Count == 0 ? "none" : string.Join(", ", names);
}
