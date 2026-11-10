namespace Kaleido.Eventing;

// Wakes the local relay as soon as events are written, so they do not wait for the next poll.
internal interface IKaleidoEventSignal
{
    void Notify();

    // True when notified, false when the wait ran out.
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

// Several notifications before the relay wakes collapse into one.
internal sealed class KaleidoEventSignal : IKaleidoEventSignal, IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        if (_semaphore.CurrentCount > 0)
        {
            return;
        }

        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Another notification won the race; the relay wakes either way.
        }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) =>
        _semaphore.WaitAsync(timeout, cancellationToken);

    public void Dispose() => _semaphore.Dispose();
}
