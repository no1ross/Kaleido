using Kaleido.Eventing;
using Kaleido.Provider.SQLite.Entities;
using Kaleido.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite.UnitTests;

// What a host's own outbox on the same database joins to commit with the state (ADR 0023).
public sealed class SqliteKaleidoTransactionTests
    : Kaleido.UnitTests.SutFixture, IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public SqliteKaleidoTransactionTests()
    {
        _connection.Open();
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private SqliteKaleidoContextDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>().UseSqlite(_connection).Options);

    private static SqliteKaleidoTransaction CreateSut(SqliteKaleidoContextDbContext dbContext) => new(dbContext);

    private static KaleidoOutboxEvent Event() =>
        new() { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessStepCompleted, Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow };

    [Fact]
    public void Current_OutsideATransition_IsNull()
    {
        using var db = CreateDbContext();

        Assert.Null(CreateSut(db).Current);
    }

    [Fact]
    public async Task Current_InsideATransition_IsItsTransactionOnTheProvidersConnection()
    {
        await using var db = CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        var current = CreateSut(db).Current;

        Assert.NotNull(current);
        Assert.Same(db.Database.GetDbConnection(), current.Connection);
    }

    // The host's outbox (another DbContext here) joins the transaction: its row commits or rolls back
    // with the provider's.
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task AnotherDbContext_JoiningCurrent_CommitsOrRollsBackWithIt(bool commit, int expectedRows)
    {
        await using var db = CreateDbContext();
        await using (var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            var current = CreateSut(db).Current;
            Assert.NotNull(current);

            await using var hostDb = new SqliteKaleidoContextDbContext(new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>().UseSqlite(current.Connection!).Options);
            await hostDb.Database.UseTransactionAsync(current, TestContext.Current.CancellationToken);
            hostDb.OutboxEvents.Add(Event().ToEntity());
            await hostDb.SaveChangesAsync(TestContext.Current.CancellationToken);

            if (commit)
            {
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }
        }

        await using var reader = CreateDbContext();
        Assert.Equal(expectedRows, await reader.OutboxEvents.CountAsync(TestContext.Current.CancellationToken));
    }
}
