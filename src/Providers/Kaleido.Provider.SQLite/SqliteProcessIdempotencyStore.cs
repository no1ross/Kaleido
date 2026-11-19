using System.Globalization;
using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite;

// The records of requests sent with an idempotency key, in the IdempotentResults table of the same
// database as the process state. Every write is one SQL statement, atomic in SQLite; finalizing is
// conditional on the admission id (#441), so it works across processes sharing the database.
internal sealed class SqliteProcessIdempotencyStore(
    SqliteKaleidoContextDbContext dbContext,
    TimeProvider time)
    : IProcessIdempotencyStore
{
    public async Task<IdempotencyRecord?> AdmitAsync(IdempotencyRecord admission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (admission.State != IdempotencyState.InProgress || admission.Replay is not null)
        {
            throw new ArgumentException("An admission must be in progress without a replay.", nameof(admission));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Expired records are deleted as new keys arrive, so the table does not grow without bound.
        var nowTicks = time.GetUtcNow().UtcTicks;
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM IdempotentResults WHERE ExpiresUtc <= {nowTicks}",
            cancellationToken);

        // One atomic insert-if-absent on the primary key: of two admissions of the same key, exactly one inserts.
        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO IdempotentResults (Key, ProcessorName, ProcessorVersion, AdmissionId, RequestHash, State, LeaseUntilUtc, ExpiresUtc)
            VALUES ({admission.Key}, {admission.ProcessorName}, {admission.ProcessorVersion}, {Text(admission.AdmissionId)}, {admission.RequestHash}, {(int)admission.State}, {admission.LeaseUntilUtc.UtcTicks}, {admission.ExpiresUtc.UtcTicks})
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

    public async Task<bool> CompleteAsync(IdempotencyRecord result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.State is not (IdempotencyState.Completed or IdempotencyState.OutcomeUnknown)
            || (result.State == IdempotencyState.Completed) != (result.Replay is not null))
        {
            throw new ArgumentException("A completed result requires a replay; an unknown outcome has none.", nameof(result));
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Only the admission that holds the key, while it is in progress; never an insert.
        var nowTicks = time.GetUtcNow().UtcTicks;
        var updated = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE IdempotentResults
            SET State = {(int)result.State},
                LeaseUntilUtc = {result.LeaseUntilUtc.UtcTicks},
                ReplayFormat = {result.Replay?.Format},
                ReplayPayload = {result.Replay?.Payload},
                ExpiresUtc = {result.ExpiresUtc.UtcTicks}
            WHERE Key = {result.Key} AND AdmissionId = {Text(result.AdmissionId)} AND State = {(int)IdempotencyState.InProgress} AND ExpiresUtc > {nowTicks}
            """,
            cancellationToken);

        return updated == 1;
    }

    public async Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();
        var nowTicks = time.GetUtcNow().UtcTicks;

        var deleted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM IdempotentResults WHERE Key = {key} AND AdmissionId = {Text(admissionId)} AND State = {(int)IdempotencyState.InProgress} AND ExpiresUtc > {nowTicks}",
            cancellationToken);

        return deleted == 1;
    }

    private static string Text(Guid admissionId) => admissionId.ToString("D", CultureInfo.InvariantCulture);

    private static IdempotencyRecord ToResult(IdempotentResultEntity entity) =>
        new()
        {
            Key = entity.Key,
            ProcessorName = entity.ProcessorName,
            ProcessorVersion = entity.ProcessorVersion,
            AdmissionId = Guid.Parse(entity.AdmissionId, CultureInfo.InvariantCulture),
            RequestHash = entity.RequestHash,
            State = entity.State,
            LeaseUntilUtc = entity.LeaseUntilUtc,
            Replay = entity.State == IdempotencyState.Completed
                ? new IdempotencyReplay(
                    entity.ReplayFormat ?? throw new KaleidoConfigurationException(SqliteErrorCodes.InvalidStoredRecord, "A completed SQLite admission has no replay format."),
                    entity.ReplayPayload ?? throw new KaleidoConfigurationException(SqliteErrorCodes.InvalidStoredRecord, "A completed SQLite admission has no replay payload."))
                : null,
            ExpiresUtc = entity.ExpiresUtc,
        };
}
