using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client;

/// <summary>
/// <see cref="IKaleidoRegistrySource"/> over the clients registered with <c>AddHttpClients()</c>, until
/// the control registry replaces it (#553): one registry fetch per client name, carrying both its Process
/// and Queryable registrations (<see cref="IKaleidoRemoteRegistry"/>, cached per client). Clients whose
/// route prefix is this service's own name are skipped — an aggregator must never fetch its own
/// registry, or the request would recurse through its own endpoint.
/// </summary>
internal sealed partial class ClientFanOutRegistrySource(
    IKaleidoClientCatalog catalog,
    IKaleidoRemoteRegistry remoteRegistry,
    ICorrelationHeaderStamper headerStamper,
    KaleidoServiceOptions serviceOptions,
    ILogger<ClientFanOutRegistrySource> logger)
    : IKaleidoRegistrySource
{
    private static readonly KaleidoRegistryStatus NoStatus = new();

    // The host merges these clients' registries with its own.
    public bool IsFullRegistry => false;

    // The clients' services publish no versions' statuses.
    public Task<KaleidoRegistryStatus> GetStatusAsync(CancellationToken cancellationToken) => Task.FromResult(NoStatus);

    public async Task<KaleidoRegistry> GetAsync(
        int? maxParallelism,
        CancellationToken cancellationToken)
    {
        var processes = new ConcurrentBag<ProcessorRegistryResponse>();
        var queryables = new ConcurrentBag<QueryableSourceResponse>();
        var errors = new ConcurrentBag<RegistryReadError>();

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

        return new KaleidoRegistry { Processes = processes.ToArray(), Queryables = queryables.ToArray(), Errors = errors.ToArray() };
    }

    private async Task LoadClientAsync(
        KaleidoClientDefinition client,
        ConcurrentBag<ProcessorRegistryResponse> processes,
        ConcurrentBag<QueryableSourceResponse> queryables,
        ConcurrentBag<RegistryReadError> errors,
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

            errors.Add(new RegistryReadError
            {
                Source = client.Name,
                Reason = "Registry fetch failed. See server logs for details.",
            });
        }
    }

    public async Task RefreshAsync(
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

    private KaleidoClientDefinition[] Clients() =>
        catalog.Clients
            .Where(x => !string.Equals(x.RoutePrefix, serviceOptions.ServiceName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Registry client {ClientName} failed: {Reason}.")]
    private static partial void LogRegistryClientFailed(ILogger logger, Exception exception, string clientName, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to invalidate registry cache for client {ClientName}.")]
    private static partial void LogInvalidateFailed(ILogger logger, Exception exception, string clientName);
}
