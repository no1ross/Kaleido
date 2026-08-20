using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Configurations;

internal sealed class NetworkConfiguration : IEntityTypeConfiguration<Network>
{
    public void Configure(EntityTypeBuilder<Network> builder)
    {
        builder.HasKey(x => x.NetworkId);

        builder.Property(x => x.NetworkCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.NetworkName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.NetworkCode)
            .IsUnique();

        builder.HasIndex(x => x.NetworkName);
    }
}
