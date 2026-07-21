using CsvHelper;
using Kaleido.Samples.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ReferenceData.ValueSets;
using System.Formats.Asn1;
using System.Globalization;
using System.Reflection.Emit;

namespace ReferenceData;

public sealed class KaleidoTestDbContext : DbContext
{
    public KaleidoTestDbContext(DbContextOptions<KaleidoTestDbContext> options)
        : base(options)
    {
    }

    public DbSet<FunctionalRecord> Records => Set<FunctionalRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FunctionalRecord>(entity =>
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
    public static async Task InitializeAsync(KaleidoTestDbContext db, string csvPath, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Records.AnyAsync(cancellationToken))
        {
            return;
        }

        var records = new FunctionalRecordCsvStore();

        await db.Records.AddRangeAsync(records.Records, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<FunctionalRecord>> LoadClientsAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);

        using var reader = new StreamReader(stream);

        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        return csv.GetRecords<FunctionalRecord>().ToList();
    }
}
