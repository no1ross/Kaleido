using Kaleido.Registry;
using Kaleido.Registry.UnitTests;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Provider.SQLite.UnitTests;

// The registry store contract (IKaleidoRegistryStoreTests, #553) on SQLite: each test has its own
// database file, and every store is its own connection to it, as each registry request (and each
// registry node) has - so the concurrent writes run as several writers on one file.
public sealed class SqliteKaleidoRegistryStoreTests
    : IKaleidoRegistryStoreTests, IDisposable
{
    private readonly string file = Path.Combine(Path.GetTempPath(), $"kaleido-registry-{Guid.NewGuid():N}.db");
    private readonly List<SqliteKaleidoRegistryDbContext> contexts = [];
    private readonly object gate = new();

    public SqliteKaleidoRegistryStoreTests()
    {
        using var context = Context();
        context.Database.EnsureCreated();
    }

    private SqliteKaleidoRegistryDbContext Context() =>
        new(new DbContextOptionsBuilder<SqliteKaleidoRegistryDbContext>().UseSqlite($"Data Source={file};Pooling=False").Options);

    private SqliteKaleidoRegistryStore CreateSut()
    {
        var context = Context();

        lock (gate)
        {
            contexts.Add(context);
        }

        return new SqliteKaleidoRegistryStore(context);
    }

    protected override IKaleidoRegistryStore CreateStore() => CreateSut();

    // Times are kept as UTC ticks and ids as text: a time with an offset comes back as the same instant,
    // in UTC, and the instance id exactly.
    [Fact]
    public async Task TimesAndIds_SurviveTheRoundTrip()
    {
        var registered = new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.FromHours(2));
        var instance = new RegistryInstanceRecord
        {
            InstanceId = Guid.NewGuid(),
            ServiceName = "radiology",
            ServiceVersion = "2026-11",
            Revision = "r1",
            RegisteredUtc = registered,
            LastHeartbeatUtc = registered.AddTicks(1),
        };
        await CreateSut().AddRegistrationAsync(new RegistryRegistrationRecord { ServiceName = "radiology", ServiceVersion = "2026-11", Revision = "r1", RegisteredUtc = registered }, "{}", Cancel);
        await CreateSut().SaveInstanceAsync(instance, Cancel);

        var contents = await CreateSut().ReadAsync(Cancel);

        var read = Assert.Single(contents.Instances);
        Assert.Equal(instance, read);
        Assert.Equal(TimeSpan.Zero, read.RegisteredUtc.Offset);
        Assert.Equal(registered, Assert.Single(contents.Registrations).RegisteredUtc);
    }

    public void Dispose()
    {
        foreach (var context in contexts)
        {
            context.Dispose();
        }

        SqliteConnection.ClearAllPools();
        File.Delete(file);
    }
}
