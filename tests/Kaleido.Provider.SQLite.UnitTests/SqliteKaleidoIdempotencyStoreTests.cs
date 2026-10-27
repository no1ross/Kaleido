using Kaleido.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kaleido.Provider.SQLite.UnitTests;

public sealed class SqliteKaleidoIdempotencyStoreTests
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

    private static SqliteKaleidoIdempotencyStore CreateSut(SqliteKaleidoContextDbContext dbContext) => new(dbContext);

    private static IdempotentResult Admission(string key, DateTimeOffset? expiresUtc = null, DateTimeOffset? leaseUntilUtc = null, string hash = "hash") =>
        new()
        {
            Key = key,
            AdmissionId = Guid.NewGuid(),
            RequestHash = hash,
            State = IdempotentResultState.InProgress,
            LeaseUntilUtc = leaseUntilUtc ?? DateTimeOffset.UtcNow.AddMinutes(5),
            ExpiresUtc = expiresUtc ?? DateTimeOffset.UtcNow.AddHours(1),
        };

    private static IdempotentResult Completed(IdempotentResult admission, string response) =>
        admission with { State = IdempotentResultState.Completed, StatusCode = 200, Response = response };

    [Fact]
    public async Task AdmitAsync_ANewKey_IsAdmitted_AndASecondAdmissionGetsTheRecordInProgress()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var lease = DateTimeOffset.UtcNow.AddMinutes(5);

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:k1", leaseUntilUtc: lease), TestContext.Current.CancellationToken));

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1", hash: "other"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.InProgress, existing.State);
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
        var expiredLease = DateTimeOffset.UtcNow.AddMinutes(-1);

        using (var before = CreateSharedDbContext(connectionString))
        {
            await CreateSut(before).AdmitAsync(Admission("svc:alice:k1", leaseUntilUtc: expiredLease), TestContext.Current.CancellationToken);
        }

        using var after = CreateSharedDbContext(connectionString);
        var existing = await CreateSut(after).AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken);

        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.InProgress, existing.State);
        Assert.True(existing.LeaseUntilUtc < DateTimeOffset.UtcNow);
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
        Assert.Equal(IdempotentResultState.Completed, existing.State);
        Assert.Equal(200, existing.StatusCode);
        Assert.Equal("""{"processId":"p"}""", existing.Response);
        Assert.Equal(admission.AdmissionId, existing.AdmissionId);
    }

    [Fact]
    public async Task CompleteAsync_WithAnUnknownOutcome_KeepsTheKeyRefusing()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var admission = Admission("svc:alice:k1");
        await sut.AdmitAsync(admission, TestContext.Current.CancellationToken);

        Assert.True(await sut.CompleteAsync(admission with { State = IdempotentResultState.OutcomeUnknown }, TestContext.Current.CancellationToken));

        Assert.Equal(IdempotentResultState.OutcomeUnknown, (await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken))?.State);
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

        Assert.Equal("{}", (await sut.AdmitAsync(Admission("svc:alice:done"), TestContext.Current.CancellationToken))?.Response);
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
                var a = Admission("svc:alice:k", expiresUtc: DateTimeOffset.UtcNow.AddMinutes(-1));
                var b = Admission("svc:alice:k");
                await CreateSut(first).AdmitAsync(a, TestContext.Current.CancellationToken);
                Assert.Null(await CreateSut(second).AdmitAsync(b, TestContext.Current.CancellationToken));

                Assert.False(await CreateSut(first).CompleteAsync(Completed(a, "{\"from\":\"a\"}"), TestContext.Current.CancellationToken));
                Assert.False(await CreateSut(first).ReleaseAsync(a.Key, a.AdmissionId, TestContext.Current.CancellationToken));

                var existing = await CreateSut(second).AdmitAsync(Admission("svc:alice:k"), TestContext.Current.CancellationToken);
                Assert.NotNull(existing);
                Assert.Equal(b.AdmissionId, existing.AdmissionId);
                Assert.Equal(IdempotentResultState.InProgress, existing.State);
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
        await sut.AdmitAsync(Admission("svc:alice:old", DateTimeOffset.UtcNow.AddMinutes(-1)), TestContext.Current.CancellationToken);

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:old"), TestContext.Current.CancellationToken));
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
}
