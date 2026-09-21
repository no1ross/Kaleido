using Kaleido.Samples.PriorAuth.Provider.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Provider.Data.Configurations;

internal sealed class ProviderLocationSpecialtyConfiguration : IEntityTypeConfiguration<ProviderLocationSpecialty>
{
    public void Configure(EntityTypeBuilder<ProviderLocationSpecialty> builder)
    {
        builder.HasKey(x => new { x.ProviderLocationId, x.MedicalSpecialtyId });

        builder.HasIndex(x => x.MedicalSpecialtyId);

        builder.HasOne(x => x.ProviderLocation)
            .WithMany(x => x.Specialties)
            .HasForeignKey(x => x.ProviderLocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
