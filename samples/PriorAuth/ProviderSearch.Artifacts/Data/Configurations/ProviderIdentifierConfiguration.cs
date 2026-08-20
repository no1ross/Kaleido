using Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.ProviderSearch.Artifacts.Data.Configurations;

internal sealed class ProviderIdentifierConfiguration : IEntityTypeConfiguration<ProviderIdentifier>
{
    public void Configure(EntityTypeBuilder<ProviderIdentifier> builder)
    {
        builder.HasKey(x => x.ProviderIdentifierId);

        builder.Property(x => x.IdentifierType)
            .HasConversion<string>();

        builder.Property(x => x.IdentifierValue)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.ProviderId);

        builder.HasIndex(x => new { x.IdentifierType, x.IdentifierValue });
    }
}
