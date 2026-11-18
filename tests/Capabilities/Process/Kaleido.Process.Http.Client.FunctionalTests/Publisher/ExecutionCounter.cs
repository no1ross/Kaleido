namespace Kaleido.Process.Http.Client.FunctionalTests.Publisher;

public sealed class ExecutionCounter
{
    private int _count;

    public int Count => Volatile.Read(ref _count);

    public void Increment() => Interlocked.Increment(ref _count);
}
