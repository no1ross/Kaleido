using System.Data.Common;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kaleido.Provider.SQLite;

/// <summary>
/// The SQLite transaction of the current process-state transition, for another writer on the same
/// database to join - typically the host's own outbox (<c>AddOutbox&lt;T&gt;()</c>) writing its rows
/// in the transaction the SQLite context store saves the state in, so both commit together (ADR 0023).
/// Registered by <c>AddSqliteContextStore</c> (scoped).
/// </summary>
/// <remarks>
/// Atomic only on the same database file: a writer on another database cannot join (no transaction
/// spans two SQLite databases).
/// </remarks>
/// <example>
/// <code>
/// public sealed class IntakeOutbox(IntakeDbContext db, ISqliteKaleidoTransaction transaction) : IKaleidoOutbox
/// {
///     public async Task AddAsync(IReadOnlyCollection&lt;KaleidoOutboxEvent&gt; events, CancellationToken cancellationToken = default)
///     {
///         if (transaction.Current is { } current)                       // inside a transition: join it
///         {
///             db.Database.SetDbConnection(current.Connection);
///             await db.Database.UseTransactionAsync(current, cancellationToken);
///         }
///
///         db.OutboxMessages.AddRange(events.Select(e => new OutboxMessage(e.EventId, e.EventType, e.Payload, e.OccurredUtc)));
///         await db.SaveChangesAsync(cancellationToken);                 // committed with the state
///     }
/// }
/// </code>
/// </example>
public interface ISqliteKaleidoTransaction
{
    /// <summary>
    /// The open transaction of the current transition (its <see cref="DbTransaction.Connection"/> is the
    /// connection to join), or <see langword="null"/> outside one.
    /// </summary>
    DbTransaction? Current { get; }
}

internal sealed class SqliteKaleidoTransaction(SqliteKaleidoContextDbContext dbContext) : ISqliteKaleidoTransaction
{
    public DbTransaction? Current => dbContext.Database.CurrentTransaction?.GetDbTransaction();
}
