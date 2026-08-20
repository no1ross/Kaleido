using Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Configurations;

internal sealed class CodeGrouperConfiguration : IEntityTypeConfiguration<CodeGrouper>
{
    public void Configure(EntityTypeBuilder<CodeGrouper> builder)
    {
        builder.HasKey(x => x.CodeGrouperId);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.GroupingType)
            .HasConversion<string>();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Source)
            .HasMaxLength(200);

        builder.Property(x => x.Version)
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.Name);

        builder.HasIndex(x => x.GroupingType);

        builder.HasIndex(x => new { x.Name, x.Version });
    }
}
