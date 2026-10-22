namespace Kaleido.Storage;

/// <summary>
/// Makes a process-state transition atomic: Kaleido saves the state
/// (<see cref="IKaleidoContextStore.SaveAsync"/>) and adds the transition's events
/// (<see cref="IKaleidoOutbox.AddAsync"/>) inside one transaction from this factory, then commits it.
/// Provided by persistence-specific packages (e.g. <c>AddSqliteContextStore</c>) or by the host for
/// its own persistence (<c>AddTransactions&lt;T&gt;()</c>); optional (ADR 0023).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Optional.</strong> Without an implementation, <c>AddKaleido()</c> registers one that does
/// nothing: each store write stands alone, so a crash between saving the state and adding its events
/// can lose those events.
/// </para>
/// <para>
/// <strong>Atomic only on one database.</strong> The transaction covers what the context store and
/// the outbox write to the database it belongs to (e.g. one EF Core <c>DbContext</c>, a MongoDB
/// session). An outbox in another database is not covered: no transaction spans two databases here.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // EF Core: one transaction on the DbContext both the context store and the outbox write to.
/// public sealed class OrdersTransactions(OrdersDbContext db) : IKaleidoTransactions
/// {
///     public async Task&lt;IKaleidoTransaction&gt; BeginAsync(CancellationToken cancellationToken) =>
///         new OrdersTransaction(await db.Database.BeginTransactionAsync(cancellationToken));
///
///     private sealed class OrdersTransaction(IDbContextTransaction transaction) : IKaleidoTransaction
///     {
///         public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
///
///         public ValueTask DisposeAsync() => transaction.DisposeAsync(); // rolls back when not committed
///     }
/// }
/// </code>
/// </example>
public interface IKaleidoTransactions
{
    /// <summary>Begins the transaction of one process-state transition.</summary>
    /// <param name="cancellationToken">Cancels beginning the transaction.</param>
    /// <returns>The transaction; disposing it without <see cref="IKaleidoTransaction.CommitAsync"/> rolls it back.</returns>
    Task<IKaleidoTransaction> BeginAsync(CancellationToken cancellationToken);
}

/// <summary>
/// The transaction of one process-state transition (<see cref="IKaleidoTransactions.BeginAsync"/>):
/// committed once the state is saved and its events are added; disposed without a commit, it rolls
/// back, and neither the state nor the events are kept.
/// </summary>
public interface IKaleidoTransaction : IAsyncDisposable
{
    /// <summary>Commits the state and the events written since the transaction began.</summary>
    /// <param name="cancellationToken">Cancels the commit (Kaleido passes <see cref="CancellationToken.None"/> once the step's outcome is final).</param>
    Task CommitAsync(CancellationToken cancellationToken);
}
