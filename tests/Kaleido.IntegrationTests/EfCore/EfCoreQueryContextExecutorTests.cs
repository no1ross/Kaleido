using System.Collections.Concurrent;
using System.Data.Common;
using Kaleido.Queryable.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.IntegrationTests.EfCore;

// IQueryContextExecutor<TView> is the extension point for provider-native execution. The default
// executor counts an async query by enumerating it (the database streams every matching row); an
// EF Core host registers an executor that uses EF's own operators, so the count is one COUNT
// statement. These tests prove the registration replaces the default and what SQL each one sends.
public sealed class EfCoreQueryContextExecutorTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly CommandRecorder _commands = new();

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();

        await using var db = new MemberDbContext(new DbContextOptionsBuilder<MemberDbContext>().UseSqlite(_connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Members.AddRange(Enumerable.Range(1, 7).Select(id => new MemberEntity { Id = id, Name = $"Member {id}", City = "Austin", JoinedUtc = new DateTime(2024, 1, id, 0, 0, 0, DateTimeKind.Utc) }));
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task AnEfCoreExecutor_CountsWithOneCountStatement_AndReturnsTheSameResult()
    {
        await using var provider = CreateProvider(services => services.AddSingleton(typeof(IQueryContextExecutor<>), typeof(EfCoreQueryContextExecutor<>)));

        var result = await QueryFirstPageAsync(provider);

        Assert.Equal([1, 2], result.Results.Select(x => x.Id));
        Assert.Equal(7, result.TotalCount);
        var commands = _commands.Texts.ToArray();
        Assert.Equal(2, commands.Length);
        Assert.Contains(commands, x => x.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(commands, x => x.Contains("LIMIT", StringComparison.OrdinalIgnoreCase));
    }

    // The executor is registered for every source of the host: a source that is not EF Core (here in
    // memory) must still be listed and counted, by LINQ.
    [Fact]
    public async Task AnEfCoreExecutor_AlsoServesASourceThatIsNotEfCore_InTheSameHost()
    {
        await using var provider = CreateProvider(services => services.AddSingleton(typeof(IQueryContextExecutor<>), typeof(EfCoreQueryContextExecutor<>)));
        using var scope = provider.CreateScope();

        // The default page of 2 is full, so the total of 3 must be counted.
        var regions = await scope.ServiceProvider.GetRequiredService<IQueryableService>()
            .QueryAsync<RegionSource, RegionQueryContext>(new QueryRequest(new QueryBody()), TestContext.Current.CancellationToken);
        var members = await QueryFirstPageAsync(provider);

        Assert.Equal(["east", "north"], regions.Results.Select(x => x.Code));
        Assert.Equal(3, regions.TotalCount);
        Assert.Equal(7, members.TotalCount);
        Assert.Contains(_commands.Texts, x => x.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TheDefaultExecutor_ReturnsTheSameResult_ButCountsByReadingEveryMatchingRow()
    {
        await using var provider = CreateProvider(_ => { });

        var result = await QueryFirstPageAsync(provider);

        Assert.Equal([1, 2], result.Results.Select(x => x.Id));
        Assert.Equal(7, result.TotalCount);
        Assert.DoesNotContain(_commands.Texts, x => x.Contains("COUNT(", StringComparison.OrdinalIgnoreCase));
    }

    private ServiceProvider CreateProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MemberDbContext>(o => o.UseSqlite(_connection).AddInterceptors(_commands));
        services.AddKaleido(new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "members",
            AuthorizationMode = KaleidoAuthorizationMode.None,
            Assemblies = [typeof(EfCoreQueryContextExecutorTests).Assembly],
            TypeFilter = type => type.Namespace == typeof(EfCoreQueryContextExecutorTests).Namespace,
        }).AddInMemoryStores();

        // After AddKaleido on purpose: the framework registers its default with TryAdd, so a host's
        // own executor wins whichever order it is registered in.
        configure(services);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    private async Task<QueryResult<MemberQueryContext>> QueryFirstPageAsync(ServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        _commands.Clear();

        // The default page of 2 is full, so the total of 7 must be counted.
        return await scope.ServiceProvider.GetRequiredService<IQueryableService>()
            .QueryAsync<MemberSource, MemberQueryContext>(new QueryRequest(new QueryBody()), TestContext.Current.CancellationToken);
    }

    // What an EF Core host registers (docs/PATTERNS.md): EF's own async operators for EF queries,
    // translated by the provider; LINQ for any other source of the host (it serves every source).
    private sealed class EfCoreQueryContextExecutor<TView> : IQueryContextExecutor<TView>
        where TView : class
    {
        public Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken = default) =>
            query.Provider is IAsyncQueryProvider
                ? EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken)
                : Task.FromResult(query.Count());

        public async Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken = default) =>
            query.Provider is IAsyncQueryProvider
                ? await EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken)
                : query.ToList();

        public IQueryable<TView> ApplyPage(IQueryable<TView> query, int size, int offset) =>
            query.Skip(offset).Take(size);
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Texts { get; private set; } = new();

        public void Clear() => Texts = new();

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Texts.Enqueue(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
