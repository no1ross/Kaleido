using System.Text;
using Kaleido.Exceptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Provider.SQLite.UnitTests;

public sealed class SqliteProcessIdempotencyStoreTests
    : Kaleido.UnitTests.SutFixture
{
    private static SqliteKaleidoContextDbContext CreateDbContext(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>()
            .UseSqlite("DataSource=:memory:");

        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        var context = new SqliteKaleidoContextDbContext(builder.Options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static SqliteKaleidoContextDbContext CreateSharedDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var context = new SqliteKaleidoContextDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

    private SqliteProcessIdempotencyStore CreateSut(SqliteKaleidoContextDbContext dbContext) => new(dbContext, time);

    private IdempotencyRecord Admission(string key, DateTimeOffset? expiresUtc = null, DateTimeOffset? leaseUntilUtc = null, string hash = "hash") =>
        new()
        {
            Key = key,
            AdmissionId = Guid.NewGuid(),
            RequestHash = hash,
            State = IdempotencyState.InProgress,
            LeaseUntilUtc = leaseUntilUtc ?? time.GetUtcNow().AddMinutes(5),
            ExpiresUtc = expiresUtc ?? time.GetUtcNow().AddHours(1),
        };

    private static IdempotencyRecord Completed(IdempotencyRecord admission, string response) =>
        admission with { State = IdempotencyState.Completed, Replay = new IdempotencyReplay("test/binary", Encoding.UTF8.GetBytes(response)) };

    [Fact]
    public async Task AdmitAsync_ANewKey_IsAdmitted_AndASecondAdmissionGetsTheRecordInProgress()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var lease = time.GetUtcNow().AddMinutes(5);

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:k1", leaseUntilUtc: lease), TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1", hash: "other"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(IdempotencyState.InProgress, existing.State);
        Assert.Equal("hash", existing.RequestHash);
        Assert.Equal(lease.UtcTicks, existing.LeaseUntilUtc.UtcTicks);
    }

    // #285: two service instances (separate connections) admitting one key at the same time: exactly
    // one wins, so a retry never runs alongside the first attempt.
    [Fact]
    public async Task AdmitAsync_FromTwoInstancesAtOnce_AdmitsExactlyOne()
    {
        var connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var first = CreateSharedDbContext(connectionString);
        using var second = CreateSharedDbContext(connectionString);

        var outcomes = await Task.WhenAll(
            CreateSut(first).AdmitAsync(Admission("svc:alice:race"), TestContext.Current.CancellationToken),
            CreateSut(second).AdmitAsync(Admission("svc:alice:race"), TestContext.Current.CancellationToken));

        Assert.Single(outcomes, x => x is null);
    }

    // #285: an admission survives a restart (a new store on the same database): a retry after the
    // service stopped mid-attempt finds the attempt, with its lease, instead of running again.
    [Fact]
    public async Task AdmitAsync_AfterARestart_FindsTheEarlierAttempt()
    {
        var connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var keepAlive = CreateSharedDbContext(connectionString);
        var expiredLease = time.GetUtcNow().AddMinutes(-1);

        using (var before = CreateSharedDbContext(connectionString))
        {
            await CreateSut(before).AdmitAsync(Admission("svc:alice:k1", leaseUntilUtc: expiredLease), TestContext.Current.CancellationToken);
        }

        using var after = CreateSharedDbContext(connectionString);
        var existing = await CreateSut(after).AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken);

        Assert.NotNull(existing);
        Assert.Equal(IdempotencyState.InProgress, existing.State);
        Assert.True(existing.LeaseUntilUtc < time.GetUtcNow());
    }

    [Fact]
    public async Task CompleteAsync_ByTheAdmission_IsAccepted_AndAdmitAsyncThenReturnsTheAnswer()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:k1");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);

        Assert.True(await sut.CompleteAsync(Completed(admission, """{"processId":"p"}"""), TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(IdempotencyState.Completed, existing.State);
        Assert.Equal("test/binary", Assert.IsType<IdempotencyReplay>(existing.Replay).Format);
        Assert.Equal("""{"processId":"p"}""", Encoding.UTF8.GetString(existing.Replay.Payload));
        Assert.Equal(admission.AdmissionId, existing.AdmissionId);
    }

    [Fact]
    public async Task CompleteAsync_RoundTripsOpaqueBinaryReplayWithoutReinterpretingIt()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:binary");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);
        byte[] payload = [0, 1, 255];

        Assert.True(await sut.CompleteAsync(admission with
        {
            State = IdempotencyState.Completed,
            Replay = new IdempotencyReplay("transport/binary-v2", payload),
        }, TestContext.Current.CancellationToken));
        payload[0] = 42;
        dbContext.ChangeTracker.Clear();
        var replay = Assert.IsType<IdempotencyReplay>(
            (await sut.AdmitAsync(Admission(admission.Key), TestContext.Current.CancellationToken))?.Replay);
        Assert.Equal("transport/binary-v2", replay.Format);
        Assert.Equal([0, 1, 255], replay.Payload);
    }

    [Fact]
    public async Task AdmitAsync_ACompletedRowWithoutReplay_RefusesAnUnsafePartialAnswer()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:corrupt");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE IdempotentResults SET State = {(int)IdempotencyState.Completed} WHERE Key = {admission.Key}",
            TestContext.Current.CancellationToken);

        var failure = await Assert.ThrowsAsync<KaleidoConfigurationException>(() =>
            sut.AdmitAsync(Admission(admission.Key), TestContext.Current.CancellationToken));

        Assert.Equal(SqliteErrorCodes.InvalidStoredRecord, failure.Code);
    }

    [Fact]
    public async Task CompleteAsync_WithAnUnknownOutcome_KeepsTheKeyRefusing()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:k1");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);

        Assert.True(await sut.CompleteAsync(admission with { State = IdempotencyState.OutcomeUnknown }, TestContext.Current.CancellationToken));

        Assert.Equal(IdempotencyState.OutcomeUnknown, (await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken))?.State);
    }

    // A finalized record is final: neither a second completion nor a release changes it.
    [Fact]
    public async Task CompleteAsync_And_ReleaseAsync_DoNotTouchAFinalizedRecord()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:done");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);
        await sut.CompleteAsync(Completed(admission, "{}"), TestContext.Current.CancellationToken);

        Assert.False(await sut.CompleteAsync(Completed(admission, "{\"again\":true}"), TestContext.Current.CancellationToken));
        Assert.False(await sut.ReleaseAsync(admission.Key, admission.AdmissionId, TestContext.Current.CancellationToken));

        Assert.Equal("{}", Encoding.UTF8.GetString(Assert.IsType<IdempotencyReplay>(
            (await sut.AdmitAsync(Admission("svc:alice:done"), TestContext.Current.CancellationToken))?.Replay).Payload));
    }

    [Fact]
    public async Task ReleaseAsync_ByTheAdmission_FreesTheKey()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:failed");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);

        Assert.True(await sut.ReleaseAsync(admission.Key, admission.AdmissionId, TestContext.Current.CancellationToken));

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:failed"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteAsync_AfterExpiryWithoutAReplacement_DoesNotFinalizeAnAbandonedAdmission()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:expired");
        Assert.Null(await sut.AdmitAsync(admission, TestContext.Current.CancellationToken));
        time.Advance(TimeSpan.FromHours(1));

        Assert.False(await sut.CompleteAsync(Completed(admission, "late"), TestContext.Current.CancellationToken));
        Assert.False(await sut.ReleaseAsync(admission.Key, admission.AdmissionId, TestContext.Current.CancellationToken));
    }

    // #441: two service instances on one database file. Instance 1 admits A, whose record expires;
    // instance 2 admits the same key (same request) as B; then A finishes late on instance 1. A's
    // completion and release are refused, and B's record is untouched.
    [Fact]
    public async Task AnAdmissionThatOutlivedItsRecord_CannotCompleteOrReleaseTheNextAdmission_AcrossInstances()
    {
        var path = Path.Join(Path.GetTempPath(), $"kaleido-idempotency-{Guid.NewGuid():N}.sqlite");

        try
        {
            using (var first = CreateSharedDbContext($"DataSource={path}"))
            using (var second = CreateSharedDbContext($"DataSource={path}"))
            {
                var a = Admission("svc:alice:k", expiresUtc: time.GetUtcNow().AddMinutes(-1));
                var b = Admission("svc:alice:k");
                await CreateSut(first).AdmitAsync(a, TestContext.Current.CancellationToken);
                Assert.Null(await CreateSut(second).AdmitAsync(b, TestContext.Current.CancellationToken));

                Assert.False(await CreateSut(first).CompleteAsync(Completed(a, "{\"from\":\"a\"}"), TestContext.Current.CancellationToken));
                Assert.False(await CreateSut(first).ReleaseAsync(a.Key, a.AdmissionId, TestContext.Current.CancellationToken));

                var existing = await CreateSut(second).AdmitAsync(Admission("svc:alice:k"), TestContext.Current.CancellationToken);
                Assert.NotNull(existing);
                Assert.Equal(b.AdmissionId, existing.AdmissionId);
                Assert.Equal(IdempotencyState.InProgress, existing.State);
                Assert.True(await CreateSut(second).CompleteAsync(Completed(b, "{\"from\":\"b\"}"), TestContext.Current.CancellationToken));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CompleteAsync_And_ReleaseAsync_ForAKeyWithoutARecord_AreRefused()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:none");

        Assert.False(await sut.CompleteAsync(Completed(admission, "{}"), TestContext.Current.CancellationToken));
        Assert.False(await sut.ReleaseAsync(admission.Key, admission.AdmissionId, TestContext.Current.CancellationToken));
        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:none"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AdmitAsync_OverAnExpiredRecord_AdmitsTheKeyAgain()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        await sut.AdmitAsync(Admission("svc:alice:old", time.GetUtcNow().AddMinutes(-1)), TestContext.Current.CancellationToken);

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:old"), TestContext.Current.CancellationToken));
    }

    // A record is kept until its retention ends, also across instances: one tick before, the key still
    // refuses; at the end it is admitted again.
    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public async Task AdmitAsync_AroundTheEndOfTheRetention(int ticksFromTheEnd, bool admittedAgain)
    {
        using var db = CreateDbContext();
        var sut = CreateSut(db);
        var expires = time.GetUtcNow().AddHours(1);
        await sut.AdmitAsync(Admission("svc:alice:kept", expires), TestContext.Current.CancellationToken);

        time.SetUtcNow(expires + TimeSpan.FromTicks(ticksFromTheEnd));

        Assert.Equal(admittedAgain, await sut.AdmitAsync(Admission("svc:alice:kept"), TestContext.Current.CancellationToken) is null);
    }

    [Fact]
    public async Task Writes_WithNullArguments_Throw()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.AdmitAsync(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.CompleteAsync(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReleaseAsync(null!, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    // #552: the record names its processor and version as given, for operators of a shared database.
    [Theory]
    [InlineData(null)]
    [InlineData("2")]
    public async Task TheProcessorAndItsVersion_AreKeptAsGiven(string? version)
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);

        await sut.AdmitAsync(Admission("svc:alice:k-version") with { ProcessorName = "svc", ProcessorVersion = version }, TestContext.Current.CancellationToken);
        var existing = await sut.AdmitAsync(Admission("svc:alice:k-version"), TestContext.Current.CancellationToken);

        Assert.Equal("svc", existing?.ProcessorName);
        Assert.Equal(version, existing?.ProcessorVersion);
    }
}
