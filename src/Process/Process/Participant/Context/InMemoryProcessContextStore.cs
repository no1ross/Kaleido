using System.Collections.Concurrent;

namespace Kaleido.Process.Participant.Context;

internal sealed class InMemoryProcessContextStore : IProcessContextStore
{
    private readonly ConcurrentDictionary<string, ParticipantContext> _contexts =
        new(StringComparer.OrdinalIgnoreCase);

    public Task<ParticipantContext> LoadAsync(string? correlationId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return Task.FromResult(
                new ParticipantContext
                {
                    ParticipantProcessId = Guid.NewGuid().ToString("N")
                });
        }

        if (_contexts.TryGetValue(
            correlationId,
            out var context))
        {
            return Task.FromResult(context);
        }

        return Task.FromResult(
            new ParticipantContext
            {
                ParticipantProcessId = correlationId
            });
    }

    public Task SaveAsync(ParticipantContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.ParticipantProcessId))
        {
            throw new InvalidOperationException(
                "ParticipantContext must contain a ParticipantProcessId before it can be saved.");
        }

        _contexts[context.ParticipantProcessId] = context;

        return Task.CompletedTask;
    }
}
