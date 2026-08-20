using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Configurations;

internal sealed class PlanNetworkConfiguration : IEntityTypeConfiguration<PlanNetwork>
{
    public void Configure(EntityTypeBuilder<PlanNetwork> builder)
    {
        builder.HasKey(x => new { x.PlanId, x.NetworkId });

        builder.HasIndex(x => x.NetworkId);

        builder.HasOne(x => x.Plan)
            .WithMany(x => x.PlanNetworks)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Network)
            .WithMany(x => x.PlanNetworks)
            .HasForeignKey(x => x.NetworkId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
