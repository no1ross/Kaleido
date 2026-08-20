using Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data.Configurations;

internal sealed class MedicalSpecialtyConfiguration : IEntityTypeConfiguration<MedicalSpecialty>
{
    public void Configure(EntityTypeBuilder<MedicalSpecialty> builder)
    {
        builder.HasKey(x => x.MedicalSpecialtyId);

        builder.Property(x => x.SpecialtyCode)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => x.SpecialtyCode)
            .IsUnique();

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}
