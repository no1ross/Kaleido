using System.Collections.Concurrent;

namespace Kaleido.Process.Context;

internal sealed class InMemoryProcessContextStore : IProcessContextStore
{
    private readonly ConcurrentDictionary<Guid, ProcessorContext> _contexts =
        new();

    public async Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_contexts.TryGetValue(
            processId,
            out var context))
        {
            return await Task.FromResult(context);
        }

        return await Task.FromResult(
            new ProcessorContext
            {
                ProcessId = processId
            });
    }

    public Task SaveAsync(ProcessorContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        _contexts[context.ProcessId] = context;

        return Task.CompletedTask;
    }
}
