using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

// Whether this instance has registered with the control registry (#553): once it has, it stays so - a
// heartbeat that fails later never makes the instance not ready.
internal interface IControlRegistryRegistrar
{
    bool HasRegistered { get; }
}

// This instance's registration with the control registry (#553). The registry never calls an instance:
// - at startup it registers (POST /registry/instances: the service, version, instance and this
//   host's own capabilities), retrying with jittered backoff (RegistryRetry, +-20%) until it succeeds - a
//   registration counts only with a lease naming the heartbeat interval, the registry's, never one of this
//   instance's - then reads the registry at once, so the host has it before its first check;
// - then it heartbeats (PUT /registry/instances/{id}/heartbeat) every interval the lease hands out, +-10%
//   so a fleet's heartbeats spread, reporting its health from local checks only (never a check calling
//   another service, tagged "remote", nor the registration check);
// - a heartbeat answered 404 registry_instance_unknown (the registry lost the instance) registers again at
//   once; any other failure is a Warning, and it goes on;
// - at shutdown it deregisters (DELETE), best effort.
internal sealed partial class ControlRegistryRegistrar(
    IHttpClientFactory httpClientFactory,
    KaleidoServiceOptions serviceOptions,
    IEnumerable<IKaleidoLocalCapabilities> capabilities,
    ICentralRegistrySource registry,
    IEnumerable<HealthCheckService> health,
    TimeProvider time,
    ILogger<ControlRegistryRegistrar> logger)
    : BackgroundService, IControlRegistryRegistrar
{
    internal static readonly TimeSpan DeregisterTimeout = TimeSpan.FromSeconds(5);

    private volatile bool _registered;
    private volatile bool _hasRegistered;

    // The heartbeat interval of the latest lease: the registry's, never this instance's.
    private TimeSpan _interval;

    public bool HasRegistered => _hasRegistered;

    // How far a wait moves from its nominal value (-1..1 of the spread); random unless a test fixes it.
    internal Func<double> Jitter { get; init; } = () => (Random.Shared.NextDouble() * 2) - 1;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (!_registered)
                {
                    if (await RegisterAsync(stoppingToken))
                    {
                        attempt = 0;
                        await registry.RefreshAsync(stoppingToken);
                        continue;
                    }

                    await Task.Delay(Spread(RegistryRetry.After(attempt++), 0.2), time, stoppingToken);
                    continue;
                }

                await Task.Delay(Spread(_interval, 0.1), time, stoppingToken);
                await HeartbeatAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        if (_registered)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DeregisterTimeout);
            await DeregisterAsync(timeout.Token);
        }
    }

    private TimeSpan Spread(TimeSpan nominal, double spread) => nominal * (1 + (spread * Jitter()));

    private async Task<bool> RegisterAsync(CancellationToken cancellationToken)
    {
        var own = capabilities.FirstOrDefault()?.Capabilities() ?? new KaleidoRegistry();
        var registration = new RegistryInstanceRegistration
        {
            ServiceName = serviceOptions.ServiceName,
            ServiceVersion = serviceOptions.ServiceVersion ?? string.Empty,
            InstanceId = serviceOptions.InstanceId,
            Processes = own.Processes,
            Queryables = own.Queryables,
        };

        using var response = await SendAsync(HttpMethod.Post, ControlRegistryContractUrls.Instances, JsonContent.Create(registration, options: KaleidoJsonOptions.Options), cancellationToken);

        // Registered only with a lease naming the interval: the registry decides how often it hears from us.
        if (response?.IsSuccessStatusCode != true || !await LeaseAsync(response, cancellationToken))
        {
            LogRegistrationFailed(logger, serviceOptions.ServiceName, (int?)response?.StatusCode);
            return false;
        }

        _registered = true;

        if (!_hasRegistered)
        {
            _hasRegistered = true;
            LogRegistered(logger, serviceOptions.ServiceName, registration.ServiceVersion, serviceOptions.InstanceId);
        }

        return true;
    }

    private async Task HeartbeatAsync(CancellationToken cancellationToken)
    {
        var heartbeat = new RegistryHeartbeat { Healthy = await HealthyAsync(cancellationToken) };
        using var response = await SendAsync(HttpMethod.Put, ControlRegistryContractUrls.Heartbeat(serviceOptions.InstanceId), JsonContent.Create(heartbeat, options: KaleidoJsonOptions.Options), cancellationToken);

        if (response?.IsSuccessStatusCode == true)
        {
            await LeaseAsync(response, cancellationToken);
        }
        else if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            LogUnknownToTheRegistry(logger, serviceOptions.InstanceId);
            _registered = false;
        }
        else
        {
            LogHeartbeatFailed(logger, serviceOptions.InstanceId, (int?)response?.StatusCode);
        }
    }

    private async Task DeregisterAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, ControlRegistryContractUrls.Instance(serviceOptions.InstanceId), content: null, cancellationToken);
        _registered = false;
    }

    // The interval the registry hands out, for the next heartbeats; false when the answer names none (an
    // answer without one keeps the last).
    private async Task<bool> LeaseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            if (await response.Content.ReadFromJsonAsync<RegistryLease>(KaleidoJsonOptions.Options, cancellationToken) is { HeartbeatInterval: var interval } && interval > TimeSpan.Zero)
            {
                _interval = interval;
                return true;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Not a lease: treated as none.
        }

        return false;
    }

    // Local checks only: never one calling another service, nor this registration's own.
    private async Task<bool> HealthyAsync(CancellationToken cancellationToken)
    {
        if (health.FirstOrDefault() is not { } checks)
        {
            return true;
        }

        var report = await checks.CheckHealthAsync(x => !x.Tags.Contains("remote") && !string.Equals(x.Name, RegistrationHealthCheck.Name, StringComparison.Ordinal), cancellationToken);

        return report.Status != HealthStatus.Unhealthy;
    }

    // The answer, or null when the registry could not be reached (logged by the caller).
    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string url, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, url.TrimStart('/')) { Content = content };
        KaleidoCallContext.MarkServiceOwn(request);

        try
        {
            return await httpClientFactory.CreateClient(CentralRegistrySource.ClientName).SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(logger, url, exception);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Registered {ServiceName} version {ServiceVersion} (instance {InstanceId}) with the control registry.")]
    private static partial void LogRegistered(ILogger logger, string serviceName, string serviceVersion, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry did not register {ServiceName} (status {StatusCode}); this instance tries again and is not ready until it is registered.")]
    private static partial void LogRegistrationFailed(ILogger logger, string serviceName, int? statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry does not know instance {InstanceId} (it restarted, failed over or removed it); registering again.")]
    private static partial void LogUnknownToTheRegistry(ILogger logger, Guid instanceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The heartbeat of instance {InstanceId} failed (status {StatusCode}); it tries again at the next interval and stays ready.")]
    private static partial void LogHeartbeatFailed(ILogger logger, Guid instanceId, int? statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The control registry could not be reached ({Url}).")]
    private static partial void LogUnreachable(ILogger logger, string url, Exception exception);
}
