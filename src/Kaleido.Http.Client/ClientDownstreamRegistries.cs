using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

/// <summary>
/// <see cref="IKaleidoDownstreamRegistries"/> over the clients registered with
/// <c>AddHttpClients()</c>: one registry fetch per client name (the Process and Queryable halves
/// share the <see cref="KaleidoRemoteRegistry"/> cache, so they never fetch twice). Clients whose
/// route prefix is this service's own name are skipped — an aggregator must never fetch its own
/// registry, or the request would recurse through its own endpoint.
/// </summary>
internal sealed partial class ClientDownstreamRegistries(
    KaleidoProcessorClientRouteOptionsMap processClientMap,
    KaleidoQueryableClientRouteOptionsMap queryableClientMap,
    IKaleidoProcessorClientFactory processClientFactory,
    IKaleidoQueryableClientFactory queryableClientFactory,
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
            ClientNames().Select(async name =>
            {
                if (gate is not null)
                {
                    await gate.WaitAsync(cancellationToken);
                }

                try
                {
                    await LoadClientAsync(name, processes, queryables, errors, cancellationToken);
                }
                finally
                {
                    gate?.Release();
                }
            }));

        return new DownstreamRegistries(processes.ToArray(), queryables.ToArray(), errors.ToArray());
    }

    private async Task LoadClientAsync(
        string name,
        ConcurrentBag<ProcessorRegistryResponse> processes,
        ConcurrentBag<QueryableSourceResponse> queryables,
        ConcurrentBag<RegistryClientError> errors,
        CancellationToken cancellationToken)
    {
        try
        {
            if (processClientMap.Options.ContainsKey(name))
            {
                foreach (var registry in await processClientFactory.GetClient(name).GetRegistryAsync(cancellationToken))
                {
                    processes.Add(registry);
                }
            }

            if (queryableClientMap.Options.ContainsKey(name))
            {
                foreach (var registry in await queryableClientFactory.GetClient(name).GetRegistryAsync(cancellationToken))
                {
                    queryables.Add(registry);
                }
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
            LogRegistryClientFailed(logger, ex, name, ex.Message);

            errors.Add(new RegistryClientError
            {
                ClientName = name,
                ClientType = "Registry",
                Reason = "Registry fetch failed. See server logs for details.",
            });
        }
    }

    public async Task InvalidateAsync(
        CancellationToken cancellationToken)
    {
        foreach (var name in ClientNames())
        {
            try
            {
                // Process and Queryable clients of one name share one cached snapshot.
                if (processClientMap.Options.ContainsKey(name))
                {
                    await processClientFactory.GetClient(name).InvalidateRegistryAsync(cancellationToken);
                }
                else
                {
                    await queryableClientFactory.GetClient(name).InvalidateRegistryAsync(cancellationToken);
                }
            }
            // Best effort per client: one client that cannot be resolved (e.g. misconfigured) or whose
            // snapshot store fails must not stop the others from being invalidated. The caller's
            // cancellation is not swallowed.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogInvalidateFailed(logger, ex, name);
            }
        }
    }

    private string[] ClientNames() =>
        processClientMap.Options
            .Concat(queryableClientMap.Options)
            .Where(x => !string.Equals(x.Value, serviceOptions.ServiceName, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registry client {ClientName} failed: {Reason}.")]
    private static partial void LogRegistryClientFailed(ILogger logger, Exception exception, string clientName, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to invalidate registry cache for client {ClientName}.")]
    private static partial void LogInvalidateFailed(ILogger logger, Exception exception, string clientName);
}
