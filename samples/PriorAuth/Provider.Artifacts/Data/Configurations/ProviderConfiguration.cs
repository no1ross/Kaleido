using Kaleido.Samples.PriorAuth.Provider.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Provider.Data.Configurations;

internal sealed class ProviderConfiguration : IEntityTypeConfiguration<ProviderInfo>
{
    public void Configure(EntityTypeBuilder<ProviderInfo> builder)
    {
        builder.HasKey(x => x.ProviderId);

        builder.Property(x => x.ProviderName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.DoingBusinessAsName)
            .HasMaxLength(200);

        builder.Property(x => x.ProviderType)
            .HasConversion<string>();

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(50);

        builder.HasIndex(x => x.ProviderName);
        builder.HasIndex(x => x.ProviderType);

        builder.HasMany(x => x.Identifiers)
            .WithOne(x => x.Provider)
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Locations)
            .WithOne(x => x.Provider)
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
