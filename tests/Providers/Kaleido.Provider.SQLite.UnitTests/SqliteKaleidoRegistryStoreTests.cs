using Kaleido.Exceptions;
using Kaleido.Registry;
using Kaleido.Registry.Server.UnitTests;
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
            BuildVersion = "1.0.0",
            RegisteredUtc = registered,
            LastHeartbeatUtc = registered.AddTicks(1),
        };
        await CreateSut().AddRegistrationAsync(new RegistryRegistrationRecord { ServiceName = "radiology", ServiceVersion = "2026-11", BuildVersion = "1.0.0", RegisteredUtc = registered }, new KaleidoRegistry(), Cancel);
        await CreateSut().SaveInstanceAsync(instance, Cancel);

        var contents = await CreateSut().ReadAsync(Cancel);

        var read = Assert.Single(contents.Instances);
        Assert.Equal(instance, read);
        Assert.Equal(TimeSpan.Zero, read.RegisteredUtc.Offset);
        Assert.Equal(registered, Assert.Single(contents.Registrations).RegisteredUtc);
    }

    [Fact]
    public async Task RegistrationWrittenBySql_MaterializesThroughTheMappedEntity()
    {
        var registration = new RegistryRegistrationRecord
        {
            ServiceName = "radiology",
            ServiceVersion = "1",
            BuildVersion = "build-1",
            RegisteredUtc = new DateTimeOffset(2026, 11, 2, 14, 0, 0, TimeSpan.FromHours(2)),
        };
        await CreateSut().AddRegistrationAsync(registration, new KaleidoRegistry
        {
            Processes = [new ProcessRegistryItem { Name = "radiology", ServiceName = "radiology", ServiceVersion = "1" }],
        }, Cancel);
        using var context = Context();

        var entity = await context.RegistryRegistrations.AsNoTracking().SingleAsync(Cancel);

        Assert.Equal((registration.ServiceName, registration.ServiceVersion, registration.BuildVersion),
            (entity.ServiceName, entity.ServiceVersion, entity.BuildVersion));
        Assert.Equal(registration.RegisteredUtc.ToUniversalTime().Ticks, entity.RegisteredUtc);
        Assert.Contains("radiology", entity.Capabilities, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_RoundTripsQueryableBindingsWithoutAQueryableRuntimeDependency()
    {
        var registration = new RegistryRegistrationRecord
        {
            ServiceName = "radiology",
            ServiceVersion = "1",
            BuildVersion = "build-query",
            RegisteredUtc = new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero),
        };
        var capabilities = new KaleidoRegistry
        {
            Queryables = [new QueryableRegistryItem
            {
                ServiceName = "radiology",
                ServiceVersion = "1",
                Name = "Directory",
                Bindings = new RegistryTransportBindings
                {
                    Http = [new RegistryHttpOperation { Operation = "query", Method = "POST", Path = "/radiology/1/queryable/directory/query" }],
                },
            }],
        };
        await CreateSut().AddRegistrationAsync(registration, capabilities, Cancel);

        var versions = await CreateSut().GetCapabilitiesAsync("radiology", "1", [registration.BuildVersion], Cancel);

        var source = Assert.Single(Assert.Single(versions).Value.Queryables);
        Assert.Equal("Directory", source.Name);
        Assert.Equal("/radiology/1/queryable/directory/query", Assert.Single(source.Bindings.Http).Path);
    }

    [Fact]
    public async Task GetCapabilitiesAsync_AnEmptyStoredCapability_RefusesToPresentAFalsePublication()
    {
        using var context = Context();
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO RegistryRegistrations (ServiceName, ServiceVersion, BuildVersion, Capabilities, RegisteredUtc) VALUES ('radiology', '1', 'broken', 'null', 0)",
            TestContext.Current.CancellationToken);

        var failure = await Assert.ThrowsAsync<KaleidoConfigurationException>(() =>
            CreateSut().GetCapabilitiesAsync("radiology", "1", ["broken"], Cancel));

        Assert.Equal(SqliteErrorCodes.InvalidStoredRecord, failure.Code);
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
