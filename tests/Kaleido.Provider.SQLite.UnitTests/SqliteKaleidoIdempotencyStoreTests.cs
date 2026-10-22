using Kaleido.Processor.Context;
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
            RequestHash = hash,
            State = IdempotentResultState.InProgress,
            LeaseUntilUtc = leaseUntilUtc ?? DateTimeOffset.UtcNow.AddMinutes(5),
            ExpiresUtc = expiresUtc ?? DateTimeOffset.UtcNow.AddHours(1),
        };

    private static IdempotentResult Completed(string key, string response) =>
        new()
        {
            Key = key,
            RequestHash = "hash",
            State = IdempotentResultState.Completed,
            StatusCode = 200,
            Response = response,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        };

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
    public async Task CompleteAsync_ThenAdmitAsync_ReturnsTheCompletedAnswer()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken);

        await sut.CompleteAsync(Completed("svc:alice:k1", """{"processId":"p"}"""), TestContext.Current.CancellationToken);

        var existing = await sut.AdmitAsync(Admission("svc:alice:k1"), TestContext.Current.CancellationToken);
        Assert.NotNull(existing);
        Assert.Equal(IdempotentResultState.Completed, existing.State);
        Assert.Equal(200, existing.StatusCode);
        Assert.Equal("""{"processId":"p"}""", existing.Response);
    }

    [Fact]
    public async Task ReleaseAsync_FreesAKeyInProgress_ButKeepsACompletedOne()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        await sut.AdmitAsync(Admission("svc:alice:failed"), TestContext.Current.CancellationToken);
        await sut.AdmitAsync(Admission("svc:alice:done"), TestContext.Current.CancellationToken);
        await sut.CompleteAsync(Completed("svc:alice:done", "{}"), TestContext.Current.CancellationToken);

        await sut.ReleaseAsync("svc:alice:failed", TestContext.Current.CancellationToken);
        await sut.ReleaseAsync("svc:alice:done", TestContext.Current.CancellationToken);

        Assert.Null(await sut.AdmitAsync(Admission("svc:alice:failed"), TestContext.Current.CancellationToken));
        Assert.Equal(IdempotentResultState.Completed, (await sut.AdmitAsync(Admission("svc:alice:done"), TestContext.Current.CancellationToken))?.State);
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
        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.ReleaseAsync(null!, TestContext.Current.CancellationToken));
    }
}
