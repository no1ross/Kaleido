using Kaleido.Process.Context;
using Kaleido.Provider.SQLite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Provider.SQLite.UnitTests;

public sealed class SqliteProcessContextStoreTests
{
    private static SqliteProcessContextDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SqliteProcessContextDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new SqliteProcessContextDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static SqliteProcessContextStore CreateSut(SqliteProcessContextDbContext dbContext) =>
        new(dbContext,
            new KaleidoServiceOptions { ServiceName = "test-svc" },
            NullLogger<SqliteProcessContextStore>.Instance);

    [Fact]
    public async Task LoadAsync_WhenProcessNotFound_ReturnsNull()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);

        var result = await sut.LoadAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsContext()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);

        var processId = Guid.NewGuid();
        var context = new ProcessorContext
        {
            ProcessId = processId,
            ProcessorName = "test-svc",
            State = ProcessExecutionState.Active,
            Steps = [],
            AvailableSteps = [],
            RequiredStep = null,
            TargetProcessorName = null,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        await sut.SaveAsync(context);

        var loaded = await sut.LoadAsync(processId);
        Assert.NotNull(loaded);
        Assert.Equal(processId, loaded.ProcessId);
    }
}
