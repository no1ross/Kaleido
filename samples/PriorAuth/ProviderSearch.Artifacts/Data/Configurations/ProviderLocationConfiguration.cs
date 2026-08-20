using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Configurations;

internal sealed class ProviderLocationConfiguration : IEntityTypeConfiguration<ProviderLocation>
{
    public void Configure(EntityTypeBuilder<ProviderLocation> builder)
    {
        builder.HasKey(x => x.ProviderLocationId);

        builder.Property(x => x.LocationName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AddressLine1)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AddressLine2)
            .HasMaxLength(200);

        builder.Property(x => x.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.StateCode)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.PostalCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(50);

        builder.HasIndex(x => x.ProviderId);
        builder.HasIndex(x => x.StateCode);
        builder.HasIndex(x => x.PostalCode);
        builder.HasIndex(x => x.LocationName);
    }
}
