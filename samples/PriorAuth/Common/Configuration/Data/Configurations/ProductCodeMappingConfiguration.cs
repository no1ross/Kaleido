using Kaleido.Samples.PriorAuth.Configuration.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Configuration.Data.Configurations;

internal sealed class ProductCodeMappingConfiguration : IEntityTypeConfiguration<ProductCodeMapping>
{
    public void Configure(EntityTypeBuilder<ProductCodeMapping> builder)
    {
        builder.HasKey(x => x.ProductCodeMappingId);

        builder.Property(x => x.CodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.CodeValue)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ProcessorName)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => new { x.CodeSystem, x.CodeValue });
    }
}
