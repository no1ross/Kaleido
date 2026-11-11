namespace Kaleido.Process.Storage;

// The default when no persistence provides transactions: begin and commit do nothing, so each store
// write stands alone (the in-memory stores, or a host that chose not to make transitions atomic).
internal sealed class NoProcessTransactions : IProcessTransactions
{
    private static readonly Task<IProcessTransaction> None = Task.FromResult<IProcessTransaction>(new NoTransaction());

    public Task<IProcessTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return None;
    }

    private sealed class NoTransaction : IProcessTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
