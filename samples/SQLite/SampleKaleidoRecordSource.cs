using Kaleido.Queryable;

namespace Kaleido.Samples.SQLite;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Functional Records",
    Description = "Sample records covering every supported field type, filter operator and match mode.",
    Source = "CSV Functional Test Data")]
[Pageable(DefaultSize = 25, MaxSize = 500)]
public sealed class SampleKaleidoRecordSource : IQuerySource<SampleKaleidoRecord>
{
    private readonly SampleKaleidoCsvData _data;

    public SampleKaleidoRecordSource(SampleKaleidoCsvData data)
    {
        _data = data;
    }

    public IQueryable<SampleKaleidoRecord> CreateQuery(QueryExecutionContext executionContext)
    {
        return _data.Records.AsQueryable();
    }
}

[QueryView(DisplayName = "Sample View", Version = "1.0.0", Description = "Sample view for functional testing.")]
public sealed class SampleKalediRecordView : IQueryViewSource<SampleKaleidoRecordSource, SampleKaleidoRecord, SampleKaleidoRecord>
{
    public IQueryable<SampleKaleidoRecord> CreateView(IQueryable<SampleKaleidoRecord> query, QueryExecutionContext executionContext)
    {
        return query;
    }
}

//public sealed class KaleidoTestDbContext : DbContext
//{
//    public KaleidoTestDbContext(DbContextOptions<KaleidoTestDbContext> options)
//        : base(options)
//    {
//    }

//    public DbSet<SampleKaleidoRecord> Records => Set<SampleKaleidoRecord>();

//    protected override void OnModelCreating(ModelBuilder modelBuilder)
//    {
//        modelBuilder.Entity<SampleKaleidoRecord>(entity =>
//        {
//            entity.ToTable("Clients");
//            entity.HasKey(x => x.Id);
//            entity.Property(x => x.ExternalId).HasConversion(v => v.ToString(), v => Guid.Parse(v));
//            entity.Property(x => x.Status).HasConversion<string>();

//            entity.HasIndex(x => x.Name);
//            entity.HasIndex(x => x.Category);
//            entity.HasIndex(x => x.Code);
//            entity.HasIndex(x => x.Region);
//            entity.HasIndex(x => x.Status);
//            entity.HasIndex(x => x.ExternalId);
//            entity.HasIndex(x => x.CreatedAt);
//            entity.HasIndex(x => x.EffectiveDate);
//        });
//    }
//}

//public static class DbInitializer
//{
//    public static async Task InitializeAsync(KaleidoTestDbContext db, CancellationToken cancellationToken = default)
//    {
//        await db.Database.EnsureCreatedAsync(cancellationToken);

//        if (await db.Records.AnyAsync(cancellationToken))
//        {
//            return;
//        }

//        var records = new SampleKaleidoCsvData();

//        await db.Records.AddRangeAsync(records.Records, cancellationToken);

//        await db.SaveChangesAsync(cancellationToken);
//    }
//}
