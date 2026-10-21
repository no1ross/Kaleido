namespace Kaleido.Storage;

// The default when no persistence provides transactions: begin and commit do nothing, so each store
// write stands alone (the in-memory stores, or a host that chose not to make transitions atomic).
internal sealed class NoKaleidoTransactions : IKaleidoTransactions
{
    private static readonly Task<IKaleidoTransaction> None = Task.FromResult<IKaleidoTransaction>(new NoTransaction());

    public Task<IKaleidoTransaction> BeginAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return None;
    }

    private sealed class NoTransaction : IKaleidoTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
