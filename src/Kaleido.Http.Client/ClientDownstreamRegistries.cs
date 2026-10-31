using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

/// <summary>
/// <see cref="IKaleidoDownstreamRegistries"/> over the clients registered with
/// <c>AddHttpClients()</c>: one registry fetch per client name, carrying both its Process and Queryable
/// registrations (<see cref="IKaleidoRemoteRegistry"/>, cached per client). Clients whose
/// route prefix is this service's own name are skipped — an aggregator must never fetch its own
/// registry, or the request would recurse through its own endpoint.
/// </summary>
internal sealed partial class ClientDownstreamRegistries(
    IKaleidoClientCatalog catalog,
    IKaleidoRemoteRegistry remoteRegistry,
    ICorrelationHeaderStamper headerStamper,
    KaleidoServiceOptions serviceOptions,
    ILogger<ClientDownstreamRegistries> logger)
    : IKaleidoDownstreamRegistries
{
    public async Task<DownstreamRegistries> GetAsync(
        int? maxParallelism,
        CancellationToken cancellationToken)
    {
        var processes = new ConcurrentBag<ProcessorRegistryResponse>();
        var queryables = new ConcurrentBag<QueryableSourceResponse>();
        var errors = new ConcurrentBag<RegistryClientError>();

        // Bounds the fan-out when a router aggregates many downstreams — null = unbounded (client
        // count is the natural bound).
        using var gate = maxParallelism is { } max
            ? new SemaphoreSlim(max)
            : null;

        await Task.WhenAll(
            Clients().Select(async client =>
            {
                if (gate is not null)
                {
                    await gate.WaitAsync(cancellationToken);
                }

                try
                {
                    await LoadClientAsync(client, processes, queryables, errors, cancellationToken);
                }
                finally
                {
                    gate?.Release();
                }
            }));

        return new DownstreamRegistries(processes.ToArray(), queryables.ToArray(), errors.ToArray());
    }

    private async Task LoadClientAsync(
        KaleidoClientDefinition client,
        ConcurrentBag<ProcessorRegistryResponse> processes,
        ConcurrentBag<QueryableSourceResponse> queryables,
        ConcurrentBag<RegistryClientError> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            var registry = await remoteRegistry.GetAsync(client, headerStamper, cancellationToken);

            foreach (var process in registry.Processes)
            {
                processes.Add(process);
            }

            foreach (var queryable in registry.Queryables)
            {
                queryables.Add(queryable);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Best effort per downstream: any failure of one client (unreachable, an error status
        // - KaleidoHttpClientException -, an unreadable body, a timeout) is reported in the
        // aggregate's errors and the others still load. The caller's own cancellation is
        // rethrown above.
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogRegistryClientFailed(logger, ex, client.Name, ex.Message);

            errors.Add(new RegistryClientError
            {
                ClientName = client.Name,
                ClientType = "Registry",
                Reason = "Registry fetch failed. See server logs for details.",
            });
        }
    }

    public async Task InvalidateAsync(
        CancellationToken cancellationToken)
    {
        foreach (var client in Clients())
        {
            try
            {
                await remoteRegistry.InvalidateAsync(client.Name, cancellationToken);
            }
            // Best effort per client: one client that cannot be resolved (e.g. misconfigured) or whose
            // snapshot store fails must not stop the others from being invalidated. The caller's
            // cancellation is not swallowed.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogInvalidateFailed(logger, ex, client.Name);
            }
        }
    }

    public async Task<AggregatedRegistryResponse?> GetServiceAsync(
        string serviceName,
        CancellationToken cancellationToken)
    {
        var client = Clients().FirstOrDefault(x => string.Equals(x.RoutePrefix, serviceName, StringComparison.OrdinalIgnoreCase));

        if (client is null)
        {
            return null;
        }

        try
        {
            return await remoteRegistry.GetAsync(client, headerStamper, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // A registry that cannot be read describes nothing (for a lookup: not permitted).
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogRegistryClientFailed(logger, ex, client.Name, ex.Message);
            return null;
        }
    }

    private KaleidoClientDefinition[] Clients() =>
        catalog.Clients
            .Where(x => !string.Equals(x.RoutePrefix, serviceOptions.ServiceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registry client {ClientName} failed: {Reason}.")]
    private static partial void LogRegistryClientFailed(ILogger logger, Exception exception, string clientName, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to invalidate registry cache for client {ClientName}.")]
    private static partial void LogInvalidateFailed(ILogger logger, Exception exception, string clientName);
}
