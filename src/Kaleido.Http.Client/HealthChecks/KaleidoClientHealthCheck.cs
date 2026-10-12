using System.Globalization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.HealthChecks;

/// <summary>
/// Health check that probes the remote Kaleido registry endpoint for a registered named client.
/// Reports <see cref="HealthStatus.Unhealthy"/> if the remote service cannot be reached or returns
/// a non-success status code, and <see cref="HealthStatus.Healthy"/> otherwise.
/// </summary>
/// <remarks>
/// Never retried and bounded by its own short timeout, so health reporting stays fast and honest; an open
/// circuit reports Unhealthy at once.
/// </remarks>
internal sealed partial class KaleidoClientHealthCheck(
    IHttpClientFactory httpClientFactory,
    string clientName,
    string registryPath,
    TimeSpan timeout,
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
            using var request = new HttpRequestMessage(HttpMethod.Get, registryPath);
            request.Options.Set(KaleidoResilienceOptions.NoRetry, value: true);

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout);

            using var response = await client.SendAsync(request, timeoutSource.Token);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy(
                    string.Create(CultureInfo.InvariantCulture, $"Kaleido remote '{clientName}' is reachable ({(int)response.StatusCode})."));
            }

            LogHealthCheckNonSuccess(logger, clientName, (int)response.StatusCode);

            return HealthCheckResult.Unhealthy(
                string.Create(CultureInfo.InvariantCulture, $"Kaleido remote '{clientName}' returned {(int)response.StatusCode}."));
        }
        // The probe's own timeout is a failed check, not a cancellation of the health check.
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LogHealthCheckFailed(logger, ex, clientName);

            return HealthCheckResult.Unhealthy(
                string.Create(CultureInfo.InvariantCulture, $"Kaleido remote '{clientName}' did not answer within {timeout.TotalSeconds:0.#} s."),
                ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogHealthCheckFailed(logger, ex, clientName);

            return HealthCheckResult.Unhealthy(
                $"Kaleido remote '{clientName}' is unreachable: {ex.Message}",
                ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido client health check for '{ClientName}' received non-success status {StatusCode}.")]
    private static partial void LogHealthCheckNonSuccess(ILogger logger, string clientName, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kaleido client health check for '{ClientName}' failed.")]
    private static partial void LogHealthCheckFailed(ILogger logger, Exception exception, string clientName);
}
