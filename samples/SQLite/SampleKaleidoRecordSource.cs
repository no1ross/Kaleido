using Kaleido.Queryable;
using Kaleido.Samples.Shared;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.SQLite;

public sealed class SampleKaleidoRecordSource : IQueryableRecordSource<SampleKaleidoRecord>
{
    private readonly KaleidoTestDbContext _store;

    public SampleKaleidoRecordSource(KaleidoTestDbContext store)
    {
        _store = store;
    }

    public IQueryable<SampleKaleidoRecord> CreateQuery(RecordExecutionContext context)
    {
        return _store.Records.AsQueryable();
    }
}

public sealed class KaleidoTestDbContext : DbContext
{
    public KaleidoTestDbContext(DbContextOptions<KaleidoTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<SampleKaleidoRecord> Records => Set<SampleKaleidoRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SampleKaleidoRecord>(entity =>
        {
            entity.ToTable("Clients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasConversion(v => v.ToString(), v => Guid.Parse(v));
            entity.Property(x => x.Status).HasConversion<string>();

            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.Code);
            entity.HasIndex(x => x.Region);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ExternalId);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.EffectiveDate);
        });
    }
}

public static class DbInitializer
{
    public static async Task InitializeAsync(KaleidoTestDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Records.AnyAsync(cancellationToken))
        {
            return;
        }

        var records = new SampleKaleidoCsvData();

        await db.Records.AddRangeAsync(records.Records, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }
}
