using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using BenchmarkDotNet.Attributes;
using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Runtime;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Benchmarks;

/// <summary>
/// #326: what a full page costs on an EF Core (SQLite) source with Kaleido's default
/// <see cref="IQueryContextExecutor{TView}"/> - which counts an async query by enumerating it - versus
/// an executor using EF Core's own <c>CountAsync</c>. A full page is the case where the query engine
/// counts: it reads the page (50 rows), then the total.
/// </summary>
[MemoryDiagnoser]
public class QueryCountBenchmarks : IDisposable
{
    private const int PageSize = 50;

    private SqliteConnection? _connection;
    private ServiceProvider? _services;
    private IQueryContextExecutor<MemberRow>? _defaultExecutor;
    private readonly EfCoreQueryContextExecutor<MemberRow> _efCoreExecutor = new();

    /// <summary>Rows in the table, every one matching the query.</summary>
    [Params(1_000, 10_000, 100_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        using (var db = CreateDb())
        {
            db.Database.EnsureCreated();
            using var transaction = _connection.BeginTransaction();
            using var insert = _connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Members (Id, Name, City) VALUES ($id, $name, 'Austin')";
            var id = insert.Parameters.Add("$id", SqliteType.Integer);
            var name = insert.Parameters.Add("$name", SqliteType.Text);

            for (var i = 1; i <= Rows; i++)
            {
                id.Value = i;
                name.Value = $"Member {i}";
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        // Kaleido's own default executor, as AddKaleido() registers it for a service with a query source
        // (and no executor of the host's own).
        _services = new ServiceCollection()
            .AddLogging()
            .AddKaleido(new ConfigurationBuilder().Build(), o =>
            {
                o.ServiceName = "benchmarks";
                o.AuthorizationMode = KaleidoAuthorizationMode.None;
                o.Assemblies = [typeof(QueryCountBenchmarks).Assembly];
                o.TypeFilter = type => type == typeof(MemberSource);
            })
            .Services
            .BuildServiceProvider();
        _defaultExecutor = _services.GetRequiredService<IQueryContextExecutor<MemberRow>>();
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _services?.Dispose();
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true, Description = "Default executor (counts by enumerating)")]
    public Task<int> DefaultExecutor() => FullPageAsync(_defaultExecutor!);

    [Benchmark(Description = "EF Core executor (SELECT COUNT)")]
    public Task<int> EfCoreExecutor() => FullPageAsync(_efCoreExecutor);

    // What QueryContextEngine does for a full page: read the page, then count.
    private async Task<int> FullPageAsync(IQueryContextExecutor<MemberRow> executor)
    {
        await using var db = CreateDb();
        var query = db.Members.AsNoTracking().Where(x => x.City == "Austin").OrderBy(x => x.Id);

        var page = await executor.ToListAsync(executor.ApplyPage(query, PageSize, 0), CancellationToken.None);
        var total = await executor.CountAsync(query, CancellationToken.None);

        return page.Count + total;
    }

    private MemberDb CreateDb() =>
        new(new DbContextOptionsBuilder<MemberDb>().UseSqlite(_connection!).Options);
}

/// <summary>A queried row: the query context of <see cref="MemberSource"/>.</summary>
public sealed class MemberRow : IQueryContext
{
    /// <summary>The key.</summary>
    [Key]
    [Description("The member's id.")]
    [Sortable]
    public int Id { get; set; }

    /// <summary>A text column.</summary>
    [Description("The member's name.")]
    public string Name { get; set; } = string.Empty;

    /// <summary>The filtered column.</summary>
    [Description("The member's city.")]
    [Filterable(FilterOperator.Equals)]
    public string City { get; set; } = string.Empty;
}

/// <summary>The source that makes AddKaleido() register the query pipeline for <see cref="MemberRow"/>.</summary>
[QuerySource(DisplayName = "Members", Description = "Benchmark members.")]
public sealed class MemberSource : IQuerySource<MemberRow>
{
    /// <inheritdoc />
    public IQueryable<MemberRow> CreateQuery(QueryExecutionContext executionContext) => Enumerable.Empty<MemberRow>().AsQueryable();
}

/// <summary>The benchmark database.</summary>
public sealed class MemberDb(DbContextOptions<MemberDb> options) : DbContext(options)
{
    /// <summary>The queried table.</summary>
    public DbSet<MemberRow> Members => Set<MemberRow>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<MemberRow>().ToTable("Members");
}

/// <summary>The executor an EF Core host registers (docs/PATTERNS.md).</summary>
public sealed class EfCoreQueryContextExecutor<TView> : IQueryContextExecutor<TView>
    where TView : class
{
    /// <inheritdoc />
    public Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken) =>
        EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken) =>
        await EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken);

    /// <inheritdoc />
    public IQueryable<TView> ApplyPage(IQueryable<TView> query, int size, int offset) =>
        query.Skip(offset).Take(size);
}
