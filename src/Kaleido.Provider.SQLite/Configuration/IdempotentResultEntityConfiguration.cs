using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Provider.SQLite.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class IdempotentResultEntityConfiguration
    : IEntityTypeConfiguration<IdempotentResultEntity>
{
    public void Configure(
        EntityTypeBuilder<IdempotentResultEntity> builder)
    {
        // Same database as the process state: no extra store to set up.
        builder.ToTable("IdempotentResults");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(400);
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Response).IsRequired();

        // Stored as ticks so expired rows can be found with a plain comparison in SQLite; the store
        // writes these columns with raw SQL (an atomic insert-if-absent), in the same units.
        builder.Property(x => x.ExpiresUtc)
            .HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        builder.Property(x => x.LeaseUntilUtc)
            .HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        builder.Property(x => x.State).HasConversion<int>();

        builder.HasIndex(x => x.ExpiresUtc);
    }
}