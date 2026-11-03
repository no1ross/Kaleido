using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Http.Registry;

// The registry's completeness (#553), separate from its readiness: every expected processor version and
// every service a published step looks up is registered, every version has a ready instance, and no instance
// is still unconfirmed after the registry resumed watching (every registry node was down). Built from the
// store only - the registry never calls an instance - and Degraded, never Unhealthy: the registry keeps
// serving what it has.
internal sealed class RegistryCompletenessHealthCheck(IKaleidoRegistrySource registrations) : IHealthCheck
{
    internal const string Name = "kaleido-registry-completeness";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var registry = await registrations.GetAsync(maxParallelism: null, cancellationToken);
        var status = await registrations.GetStatusAsync(cancellationToken);
        var unavailable = Versions(status, RegistryCapabilityStatus.Unavailable);
        var unknown = Versions(status, RegistryCapabilityStatus.Unknown);
        var unconfirmed = status.Services.Sum(x => x.UnknownInstances);
        var missing = registry.Missing.Select(x => x.ServiceVersion is null ? x.ServiceName : $"{x.ServiceName}/{x.ServiceVersion}").ToList();
        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["missing"] = missing,
            ["unavailable"] = unavailable,
            ["unknown"] = unknown,
            ["unconfirmedInstances"] = unconfirmed,
        };

        return missing.Count == 0 && unavailable.Count == 0 && unconfirmed == 0
            ? HealthCheckResult.Healthy("Every expected service version is registered and has a ready instance.", data)
            : HealthCheckResult.Degraded(
                string.Create(CultureInfo.InvariantCulture, $"Missing: {Join(missing)}; no ready instance: {Join(unavailable)}; waiting for {unconfirmed} instance(s) to report since the registry resumed (unknown: {Join(unknown)})."),
                data: data);
    }

    private static List<string> Versions(KaleidoRegistryStatus status, RegistryCapabilityStatus wanted) =>
        [.. status.Services.Where(x => x.Status == wanted).Select(x => $"{x.ServiceName}/{x.ServiceVersion}")];

    private static string Join(List<string> names) => names.Count == 0 ? "none" : string.Join(", ", names);
}
