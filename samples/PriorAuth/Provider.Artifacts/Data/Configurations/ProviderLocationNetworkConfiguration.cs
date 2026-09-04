using Kaleido.Samples.PriorAuth.Provider.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Provider.Data.Configurations;

internal sealed class ProviderLocationNetworkConfiguration : IEntityTypeConfiguration<ProviderLocationNetwork>
{
    public void Configure(EntityTypeBuilder<ProviderLocationNetwork> builder)
    {
        builder.HasKey(x => new { x.ProviderLocationId, x.NetworkId });

        builder.HasIndex(x => x.NetworkId);

        builder.HasOne(x => x.ProviderLocation)
            .WithMany(x => x.Networks)
            .HasForeignKey(x => x.ProviderLocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
