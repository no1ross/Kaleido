using Kaleido.Samples.PriorAuth.History.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.History.Data.Configurations;

internal sealed class PriorAuthRecordConfiguration : IEntityTypeConfiguration<PriorAuthRecord>
{
    public void Configure(EntityTypeBuilder<PriorAuthRecord> builder)
    {
        builder.HasKey(x => x.ProcessId);

        builder.HasIndex(x => x.PriorAuthorizationId);

        builder.HasIndex(x => x.UpdatedUtc);

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
