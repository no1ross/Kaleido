using Kaleido.Processor.Context;
using Kaleido.Provider.SQLite.Entities;
using Kaleido.Storage;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

// The records of requests sent with an idempotency key, in the IdempotentResults table of the same
// database as the process state. Every write is one SQL statement, atomic in SQLite.
internal sealed class SqliteKaleidoIdempotencyStore(
    SqliteKaleidoContextDbContext dbContext)
    : IKaleidoIdempotencyStore
{
    public async Task<IdempotentResult?> AdmitAsync(IdempotentResult admission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admission);

        // Expired records are deleted as new keys arrive, so the table does not grow without bound.
        var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM IdempotentResults WHERE ExpiresUtc <= {nowTicks}",
            cancellationToken);

        // One atomic insert-if-absent on the primary key: of two admissions of the same key, exactly one inserts.
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO IdempotentResults (Key, RequestHash, State, LeaseUntilUtc, StatusCode, Response, ExpiresUtc)
            VALUES ({admission.Key}, {admission.RequestHash}, {(int)admission.State}, {admission.LeaseUntilUtc.UtcTicks}, {admission.StatusCode}, {admission.Response}, {admission.ExpiresUtc.UtcTicks})
            ON CONFLICT(Key) DO NOTHING
            """,
            cancellationToken);

        if (inserted == 1)
        {
            return null;
        }

        var existing = await dbContext.IdempotentResults
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == admission.Key, cancellationToken);

        // Gone between the insert and the read (released or expired just then): admit again.
        return existing is null
            ? await AdmitAsync(admission, cancellationToken)
            : ToResult(existing);
    }

    public async Task CompleteAsync(IdempotentResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO IdempotentResults (Key, RequestHash, State, LeaseUntilUtc, StatusCode, Response, ExpiresUtc)
            VALUES ({result.Key}, {result.RequestHash}, {(int)result.State}, {result.LeaseUntilUtc.UtcTicks}, {result.StatusCode}, {result.Response}, {result.ExpiresUtc.UtcTicks})
            ON CONFLICT(Key) DO UPDATE SET
                RequestHash = excluded.RequestHash,
                State = excluded.State,
                LeaseUntilUtc = excluded.LeaseUntilUtc,
                StatusCode = excluded.StatusCode,
                Response = excluded.Response,
                ExpiresUtc = excluded.ExpiresUtc
            """,
            cancellationToken);
    }

    public async Task ReleaseAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM IdempotentResults WHERE Key = {key} AND State = {(int)IdempotentResultState.InProgress}",
            cancellationToken);
    }

    private static IdempotentResult ToResult(IdempotentResultEntity entity) =>
        new()
        {
            Key = entity.Key,
            RequestHash = entity.RequestHash,
            State = entity.State,
            LeaseUntilUtc = entity.LeaseUntilUtc,
            StatusCode = entity.StatusCode,
            Response = entity.Response,
            ExpiresUtc = entity.ExpiresUtc,
        };
}
