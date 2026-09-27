using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.HealthChecks;

/// <summary>
/// Health check that probes the remote Kaleido registry endpoint for a registered named client.
/// Reports <see cref="HealthStatus.Unhealthy"/> if the remote service cannot be reached or returns
/// a non-success status code, and <see cref="HealthStatus.Healthy"/> otherwise.
/// </summary>
internal sealed class KaleidoClientHealthCheck(
    IHttpClientFactory httpClientFactory,
    string clientName,
    string registryPath,
    ILogger<KaleidoClientHealthCheck> logger)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient(clientName);
            using var response = await client.GetAsync(registryPath, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy(
                    $"Kaleido remote '{clientName}' is reachable ({(int)response.StatusCode}).");
            }

            logger.LogWarning(
                "Kaleido client health check for '{ClientName}' received non-success status {StatusCode}.",
                clientName,
                (int)response.StatusCode);

            return HealthCheckResult.Unhealthy(
                $"Kaleido remote '{clientName}' returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "Kaleido client health check for '{ClientName}' failed.",
                clientName);

            return HealthCheckResult.Unhealthy(
                $"Kaleido remote '{clientName}' is unreachable: {ex.Message}",
                ex);
        }
    }
}
