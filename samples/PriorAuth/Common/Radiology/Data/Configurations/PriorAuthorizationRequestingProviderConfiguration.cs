using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Configurations;

internal sealed class PriorAuthorizationRequestingProviderConfiguration : IEntityTypeConfiguration<PriorAuthorizationRequestingProvider>
{
    public void Configure(EntityTypeBuilder<PriorAuthorizationRequestingProvider> builder)
    {
        builder.HasKey(x => x.PriorAuthorizationId);

        builder.Property(x => x.ProviderName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.LocationName)
            .HasMaxLength(200);

        builder.HasIndex(x => x.ProviderId);

        builder.HasIndex(x => x.ProviderLocationId);
    }
}
