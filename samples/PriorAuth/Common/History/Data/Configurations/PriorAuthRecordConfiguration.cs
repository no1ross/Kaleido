using Kaleido.Samples.PriorAuth.History.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.History.Data.Configurations;

internal sealed class PriorAuthRecordConfiguration : IEntityTypeConfiguration<PriorAuthRecord>
{
    public void Configure(EntityTypeBuilder<PriorAuthRecord> builder)
    {
        builder.HasKey(x => x.PriorAuthRecordId);

        builder.HasIndex(x => x.ProcessId)
            .IsUnique();

        builder.HasIndex(x => x.LastUpdatedUtc);

        builder.Property(x => x.CreatedUtc)
            .HasConversion<long>();

        builder.Property(x => x.LastUpdatedUtc)
            .HasConversion<long>();

        builder.Property(x => x.ProcessorName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MemberNumber)
            .HasMaxLength(50);

        builder.Property(x => x.MemberDisplayName)
            .HasMaxLength(200);

        builder.Property(x => x.PrimaryProcedureCode)
            .HasMaxLength(50);

        builder.Property(x => x.PrimaryProcedureDescription)
            .HasMaxLength(500);
    }
}
