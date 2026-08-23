using System.Collections.Concurrent;

namespace Kaleido.Process.Participant.Context;

internal sealed class InMemoryProcessContextStore : IProcessContextStore
{
    private readonly ConcurrentDictionary<Guid, ParticipantContext> _contexts =
        new();

    public async Task<ParticipantContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_contexts.TryGetValue(
            processId,
            out var context))
        {
            return await Task.FromResult(context);
        }

        return await Task.FromResult(
            new ParticipantContext
            {
                ProcessId = processId
            });
    }

    public Task SaveAsync(ParticipantContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        _contexts[context.ProcessId] = context;

        return Task.CompletedTask;
    }
}
