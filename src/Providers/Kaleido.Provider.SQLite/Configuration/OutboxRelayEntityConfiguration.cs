using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Provider.SQLite.Configuration;

[ExcludeFromCodeCoverage]
internal sealed class OutboxRelayEntityConfiguration
    : IEntityTypeConfiguration<OutboxRelayEntity>
{
    public void Configure(
        EntityTypeBuilder<OutboxRelayEntity> builder)
    {
        // Same database as the outbox: the writers' health check reads the heartbeats there.
        builder.ToTable("OutboxRelays");
        builder.HasKey(x => x.InstanceId);
        builder.Property(x => x.InstanceId).HasMaxLength(32);
        builder.Property(x => x.ServiceName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Publisher).HasMaxLength(400).IsRequired();
        builder.HasIndex(x => x.SeenUtc);
    }
}
