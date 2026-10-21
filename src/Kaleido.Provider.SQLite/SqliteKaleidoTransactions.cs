using Kaleido.Storage;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kaleido.Provider.SQLite;

// The transactions of process-state transitions on SQLite (registered by AddSqliteContextStore): one
// database transaction on the shared SqliteKaleidoContextDbContext, so the context store and the
// outbox store - and a host's own outbox that joins it (ISqliteKaleidoTransaction) - commit together.
internal sealed class SqliteKaleidoTransactions(SqliteKaleidoContextDbContext dbContext) : IKaleidoTransactions
{
    public async Task<IKaleidoTransaction> BeginAsync(CancellationToken cancellationToken = default) =>
        new Transaction(await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Transaction(IDbContextTransaction transaction) : IKaleidoTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        // Without a commit, disposing rolls the transaction back.
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
