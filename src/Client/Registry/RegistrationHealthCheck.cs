using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kaleido.Client.Registry;

// Readiness (#553): an instance is ready once it has registered with the control registry and read it
// once - before that, nothing on this host knows the registry. A later failure (a heartbeat, a read) never
// makes it not ready: a registry outage must not pull every service out of its load balancer.
internal sealed class RegistrationHealthCheck(IControlRegistryRegistrar registrar, ICentralRegistrySource registry) : IHealthCheck
{
    internal const string Name = "kaleido-registration";

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            (registrar.HasRegistered, registry.HasRead) switch
            {
                (true, true) => HealthCheckResult.Healthy("Registered with the control registry, which this host has read."),
                (false, _) => HealthCheckResult.Unhealthy("Not registered with the control registry yet."),
                _ => HealthCheckResult.Unhealthy("Registered, but the control registry has not been read yet."),
            });
}
