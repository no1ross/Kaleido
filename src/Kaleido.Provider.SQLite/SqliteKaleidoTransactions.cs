using Kaleido.Storage;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kaleido.Provider.SQLite;

// The transactions of process-state transitions on SQLite (registered by AddSqliteContextStore): one
// database transaction on the shared SqliteKaleidoContextDbContext, so the context store and the
// outbox store - and a host's own outbox that joins it (ISqliteKaleidoTransaction) - commit together.
internal sealed class SqliteKaleidoTransactions(SqliteKaleidoContextDbContext dbContext) : IKaleidoTransactions
{
    public async Task<IKaleidoTransaction> BeginAsync(CancellationToken cancellationToken) =>
        new Transaction(dbContext, await dbContext.Database.BeginTransactionAsync(cancellationToken));

    private sealed class Transaction(SqliteKaleidoContextDbContext dbContext, IDbContextTransaction transaction) : IKaleidoTransaction
    {
        private bool _committed;

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await transaction.CommitAsync(cancellationToken);
            _committed = true;
        }

        // Without a commit, disposing rolls the transaction back, and the context forgets what it
        // tracked in it: the entities still hold the rolled-back values (a process at the version it
        // was not saved at), which a later save in the same scope would read as a conflict (#513).
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();

            if (!_committed)
            {
                dbContext.ChangeTracker.Clear();
            }
        }
    }
}
