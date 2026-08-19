using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Configurations;

internal sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.HasKey(x => x.PlanId);

        builder.Property(x => x.PlanId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.PlanName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.StateCode)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.LineOfBusiness)
            .HasConversion<string>();

        builder.HasIndex(x => x.PlanName);

        builder.HasIndex(x => x.LineOfBusiness);

        builder.HasIndex(x => x.StateCode);

        builder.HasOne(x => x.State)
            .WithMany(x => x.Plans)
            .HasForeignKey(x => x.StateCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
