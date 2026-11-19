namespace Kaleido.Process.Storage;

/// <summary>
/// Makes a process-state transition atomic: Kaleido saves the state
/// (<see cref="IProcessContextStore.SaveAsync"/>) and adds the transition's events
/// to the outbox (<see cref="IKaleidoOutboxStore.AddAsync"/>) inside one transaction from this factory, then commits it.
/// Provided by persistence-specific packages (e.g. <c>AddSqliteContextProvider</c>) or by the host for
/// its own persistence (<c>AddTransactions&lt;T&gt;()</c>); optional (ADR 0023).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Optional.</strong> Without an implementation, <c>AddProcess()</c> registers one that does
/// nothing: each store write stands alone, so a crash between saving the state and adding its events
/// can lose those events.
/// </para>
/// <para>
/// <strong>Atomic only when both stores join one provider session/transaction.</strong> The state
/// and outbox writes must share that transaction (e.g. one EF Core <c>DbContext</c> or a MongoDB
/// session). Merely using the same database does not join them; no distributed transaction spans two databases here.
/// Its events are written when they are added, so if the commit then fails, they are still relayed
/// for a state that was never saved; a rollback cannot remove them. Keep the outbox on the context
/// store's database to avoid this.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // EF Core: one transaction on the DbContext both the context store and the outbox write to.
/// public sealed class OrdersTransactions(OrdersDbContext db) : IProcessTransactions
/// {
///     public async Task&lt;IProcessTransaction&gt; BeginAsync(CancellationToken cancellationToken) =>
///         new OrdersTransaction(await db.Database.BeginTransactionAsync(cancellationToken));
///
///     private sealed class OrdersTransaction(IDbContextTransaction transaction) : IProcessTransaction
///     {
///         public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
///
///         public ValueTask DisposeAsync() => transaction.DisposeAsync(); // rolls back when not committed
///     }
/// }
/// </code>
/// </example>
public interface IProcessTransactions
{
    /// <summary>Begins the transaction of one process-state transition.</summary>
    /// <param name="cancellationToken">Cancels beginning the transaction.</param>
    /// <returns>The transaction; disposing it without <see cref="IProcessTransaction.CommitAsync"/> rolls it back.</returns>
    Task<IProcessTransaction> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The transaction of one process-state transition (<see cref="IProcessTransactions.BeginAsync"/>):
/// committed once the state is saved and its events are added; disposed without a commit, it rolls
/// back, and neither the state nor the events are kept.
/// </summary>
public interface IProcessTransaction : IAsyncDisposable
{
    /// <summary>Commits the state and the events written since the transaction began.</summary>
    /// <param name="cancellationToken">Cancels the commit (Kaleido passes <see cref="CancellationToken.None"/> once the step's outcome is final).</param>
    Task CommitAsync(CancellationToken cancellationToken);
}
