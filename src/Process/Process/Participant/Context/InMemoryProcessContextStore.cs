using System.Collections.Concurrent;

namespace Kaleido.Process.Participant.Context;

internal sealed class InMemoryProcessContextStore
    : IProcessContextStore
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
                    CorrelationId = Guid.NewGuid().ToString("N")
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
                CorrelationId = correlationId
            });
    }

    public Task SaveAsync(ParticipantContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(context.CorrelationId))
        {
            throw new InvalidOperationException(
                "ParticipantContext must contain a CorrelationId before it can be saved.");
        }

        _contexts[context.CorrelationId] = context;

        return Task.CompletedTask;
    }
}
