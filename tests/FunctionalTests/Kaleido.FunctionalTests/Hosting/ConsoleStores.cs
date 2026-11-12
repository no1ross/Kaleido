using Kaleido.Process.Context;
using Kaleido.Process.Exceptions;
using Kaleido.Process.Storage;
using Kaleido.Storage;

namespace Kaleido.FunctionalTests.Hosting;

internal sealed class ConsoleProcessStore : IProcessContextStore
{
    private readonly Dictionary<ProcessKey, ProcessContext> _contexts = [];
    private readonly object _sync = new();

    public Task<ProcessContext?> LoadAsync(ProcessKey key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return Task.FromResult(_contexts.GetValueOrDefault(key));
        }
    }

    public Task<ProcessContext> SaveAsync(ProcessContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            var current = _contexts.GetValueOrDefault(context.Key);
            if ((current?.Version ?? 0) != context.Version)
            {
                throw new ProcessConcurrencyException(context.ProcessId);
            }

            var saved = context with { Version = context.Version + 1 };
            _contexts[context.Key] = saved;
            return Task.FromResult(saved);
        }
    }
}

internal sealed class ConsoleOutboxStore : IKaleidoOutboxStore
{
    public List<KaleidoOutboxEvent> Events { get; } = [];

    public Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Events.AddRange(events);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RecordRelayAsync(KaleidoOutboxRelay relay, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<KaleidoOutboxStatus> GetStatusAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult(new KaleidoOutboxStatus { DueCount = Events.Count });
}
