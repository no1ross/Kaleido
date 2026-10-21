using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite.UnitTests;

// The transaction of a process-state transition on SQLite: one database transaction on the DbContext
// the stores share (their atomicity end to end: Kaleido.IntegrationTests/SqliteTransitionTests).
public sealed class SqliteKaleidoTransactionsTests
    : Kaleido.UnitTests.SutFixture, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteKaleidoTransactionsTests()
    {
        _connection.Open();
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private SqliteKaleidoContextDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>().UseSqlite(_connection).Options);

    private static SqliteKaleidoTransactions CreateSut(SqliteKaleidoContextDbContext dbContext) => new(dbContext);

    // A write on the DbContext, as a store makes it.
    private static Task<int> WriteAsync(SqliteKaleidoContextDbContext db) =>
        db.Database.ExecuteSqlRawAsync(
            "INSERT INTO OutboxEvents (EventId, EventType, Payload, OccurredUtc, Attempts, NextAttemptUtc, LeaseUntil, IsParked) VALUES ('" + Guid.NewGuid() + "', 'test', 'payload', 0, 0, 0, 0, 0)",
            TestContext.Current.CancellationToken);

    private async Task<int> CommittedRowsAsync()
    {
        await using var reader = CreateDbContext();
        return await reader.OutboxEvents.CountAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task BeginAsync_OpensATransactionOnTheSharedDbContext()
    {
        await using var db = CreateDbContext();

        await using var transaction = await CreateSut(db).BeginAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task CommitAsync_KeepsTheWritesMadeInTheTransaction()
    {
        await using var db = CreateDbContext();

        await using (var transaction = await CreateSut(db).BeginAsync(TestContext.Current.CancellationToken))
        {
            await WriteAsync(db);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, await CommittedRowsAsync());
        Assert.Null(db.Database.CurrentTransaction);
    }

    // Disposed without a commit (a failure before it): the writes are rolled back.
    [Fact]
    public async Task DisposeAsync_WithoutACommit_RollsTheWritesBack()
    {
        await using var db = CreateDbContext();

        await using (await CreateSut(db).BeginAsync(TestContext.Current.CancellationToken))
        {
            await WriteAsync(db);
        }

        Assert.Equal(0, await CommittedRowsAsync());
        Assert.Null(db.Database.CurrentTransaction);
    }
}
