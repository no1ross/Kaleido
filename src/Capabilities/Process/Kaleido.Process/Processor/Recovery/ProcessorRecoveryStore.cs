using System.Collections.Concurrent;

namespace Kaleido.Processor.Recovery;

// The step outcomes waiting to be saved (#537): one per process, in memory, bounded. While a process
// has one, no step runs on it (ProcessorRuntime), so its handler is never run a second time.
internal interface IProcessorRecoveryStore
{
    // Keeps the outcome; false when the store is full or the process already has one.
    bool TryRecord(PendingOutcome outcome);

    bool IsPending(Guid processId);

    // The outcomes whose next attempt is due, oldest first.
    IReadOnlyList<PendingOutcome> Due(DateTimeOffset now);

    // Replaces the process's outcome (its next attempt).
    void Reschedule(PendingOutcome outcome);

    void Remove(Guid processId);
}

internal sealed class ProcessorRecoveryStore : IProcessorRecoveryStore
{
    // Enough for a store outage on a busy instance; past it an outcome is answered as not saved (500).
    internal const int Capacity = 1000;

    private readonly ConcurrentDictionary<Guid, PendingOutcome> _pending = new();

    public bool TryRecord(PendingOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        return _pending.Count < Capacity && _pending.TryAdd(outcome.Context.ProcessId, outcome);
    }

    public bool IsPending(Guid processId) => _pending.ContainsKey(processId);

    public IReadOnlyList<PendingOutcome> Due(DateTimeOffset now) =>
        [.. _pending.Values.Where(x => x.NextAttemptUtc <= now).OrderBy(x => x.RecordedUtc)];

    public void Reschedule(PendingOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        _pending[outcome.Context.ProcessId] = outcome;
    }

    public void Remove(Guid processId) => _pending.TryRemove(processId, out _);
}
