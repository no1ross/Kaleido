using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Provider.SQLite.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class OutboxEventEntityConfiguration
    : IEntityTypeConfiguration<OutboxEventEntity>
{
    public void Configure(
        EntityTypeBuilder<OutboxEventEntity> builder)
    {
        // Same database as the process state, so a step's event is saved in the same transaction.
        builder.ToTable("OutboxEvents");
        builder.HasKey(x => x.EventId);
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.LastError).HasMaxLength(SqliteKaleidoContextStore.MaxErrorLength);
        builder.Property(x => x.LeaseOwner).HasMaxLength(64);

        // Claims look for due, unparked, unleased entries, oldest first.
        builder.HasIndex(x => new { x.IsParked, x.NextAttemptUtc });
        builder.HasIndex(x => x.LeaseUntil);
    }
}
