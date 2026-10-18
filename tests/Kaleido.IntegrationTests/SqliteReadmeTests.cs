using Kaleido.Processor.Context;
using Kaleido.Provider.SQLite;
using Kaleido.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests;

// The SQLite provider README's "Using the example implementation", followed literally against a
// fresh database file (#323): registration, then the host creates the schema, then the store works.
public sealed class SqliteReadmeTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"kaleido-readme-{Guid.NewGuid():N}.sqlite");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }

    [Fact]
    public async Task TheReadmeSequence_OnAFreshDatabase_SavesAndLoadsAProcess()
    {
        Assert.False(File.Exists(_databasePath));
        await using var provider = BuildProvider();

        // The store does not create its tables: the host does, once, at startup.
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync(Cancel);
        }

        var processId = Guid.NewGuid();
        await using (var scope = provider.CreateAsyncScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<IKaleidoContextStore>()
                .SaveAsync(new ProcessorContext { ProcessId = processId, ProcessorName = "myservice" }, [], Cancel);
            Assert.Equal(1, saved.Version);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<IKaleidoContextStore>().LoadAsync(processId, Cancel);
            Assert.NotNull(loaded);
            Assert.Equal(1, loaded.Version);
        }
    }

    // Why the README's EnsureCreatedAsync step is needed: registration alone creates no tables.
    [Fact]
    public async Task WithoutTheSchemaStep_TheFirstSaveFails()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();

        await Assert.ThrowsAsync<SqliteException>(() =>
            scope.ServiceProvider.GetRequiredService<IKaleidoContextStore>()
                .SaveAsync(new ProcessorContext { ProcessId = Guid.NewGuid(), ProcessorName = "myservice" }, [], Cancel));
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddKaleido(new ConfigurationBuilder().Build(), o =>
            {
                o.ServiceName = "myservice";
                o.Assemblies = [typeof(SqliteReadmeTests).Assembly];
                o.TypeFilter = _ => false;
            })
            .UseSqliteKaleidoContextStore($"Data Source={_databasePath}");

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }
}
