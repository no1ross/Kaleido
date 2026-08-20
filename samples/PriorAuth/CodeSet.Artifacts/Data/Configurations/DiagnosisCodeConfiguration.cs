using Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Configurations;

internal sealed class DiagnosisCodeConfiguration : IEntityTypeConfiguration<DiagnosisCode>
{
    public void Configure(EntityTypeBuilder<DiagnosisCode> builder)
    {
        builder.HasKey(x => x.DiagnosisCodeId);

        builder.Property(x => x.CodeValue)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.ShortDescription)
            .HasMaxLength(250)
            .IsRequired();

        builder.Property(x => x.LongDescription)
            .HasMaxLength(4000);

        builder.HasIndex(x => x.CodeValue);

        builder.HasIndex(x => x.CodeSystem);

        builder.HasIndex(x => new { x.CodeSystem, x.CodeValue })
            .IsUnique();
    }
}
