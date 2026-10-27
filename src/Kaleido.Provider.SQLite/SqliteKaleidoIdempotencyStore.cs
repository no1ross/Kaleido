using System.Globalization;
using Kaleido.Provider.SQLite.Entities;
using Kaleido.Storage;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

// The records of requests sent with an idempotency key, in the IdempotentResults table of the same
// database as the process state. Every write is one SQL statement, atomic in SQLite; finalizing is
// conditional on the admission id (#441), so it works across processes sharing the database.
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
            INSERT INTO IdempotentResults (Key, AdmissionId, RequestHash, State, LeaseUntilUtc, StatusCode, Response, ExpiresUtc)
            VALUES ({admission.Key}, {Text(admission.AdmissionId)}, {admission.RequestHash}, {(int)admission.State}, {admission.LeaseUntilUtc.UtcTicks}, {admission.StatusCode}, {admission.Response}, {admission.ExpiresUtc.UtcTicks})
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

    public async Task<bool> CompleteAsync(IdempotentResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Only the admission that holds the key, while it is in progress; never an insert.
        var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE IdempotentResults
            SET State = {(int)result.State},
                LeaseUntilUtc = {result.LeaseUntilUtc.UtcTicks},
                StatusCode = {result.StatusCode},
                Response = {result.Response},
                ExpiresUtc = {result.ExpiresUtc.UtcTicks}
            WHERE Key = {result.Key} AND AdmissionId = {Text(result.AdmissionId)} AND State = {(int)IdempotentResultState.InProgress}
            """,
            cancellationToken);

        return updated == 1;
    }

    public async Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM IdempotentResults WHERE Key = {key} AND AdmissionId = {Text(admissionId)} AND State = {(int)IdempotentResultState.InProgress}",
            cancellationToken);

        return deleted == 1;
    }

    private static string Text(Guid admissionId) => admissionId.ToString("D", CultureInfo.InvariantCulture);

    private static IdempotentResult ToResult(IdempotentResultEntity entity) =>
        new()
        {
            Key = entity.Key,
            AdmissionId = Guid.Parse(entity.AdmissionId, CultureInfo.InvariantCulture),
            RequestHash = entity.RequestHash,
            State = entity.State,
            LeaseUntilUtc = entity.LeaseUntilUtc,
            StatusCode = entity.StatusCode,
            Response = entity.Response,
            ExpiresUtc = entity.ExpiresUtc,
        };
}
