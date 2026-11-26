using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Registry.Client;

// Readiness (#553): a publisher is ready once it has registered and read the Registry; a consumer-only
// host is ready after its first read. Later heartbeat/read failures never undo that readiness.
internal sealed class RegistrationHealthCheck(IControlRegistryRegistrar registrar, ICentralRegistrySource registry) : IHealthCheck
{
    internal const string Name = "kaleido-registration";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            (registrar.Publishes, registrar.HasRegistered, registry.HasRead) switch
            {
                (false, _, true) => HealthCheckResult.Healthy("The consumer-only host has read the control registry."),
                (false, _, false) => HealthCheckResult.Unhealthy("The control registry has not been read yet."),
                (true, true, true) => HealthCheckResult.Healthy("Registered with the control registry, which this host has read."),
                (true, false, _) => HealthCheckResult.Unhealthy("Not registered with the control registry yet."),
                _ => HealthCheckResult.Unhealthy("Registered, but the control registry has not been read yet."),
            });
}
