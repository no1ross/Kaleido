using Kaleido.Samples.PriorAuth.Intake.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Data.Configurations;

internal sealed class PriorAuthorizationRequestedServiceConfiguration : IEntityTypeConfiguration<PriorAuthorizationRequestedService>
{
    public void Configure(EntityTypeBuilder<PriorAuthorizationRequestedService> builder)
    {
        builder.HasKey(x => x.PriorAuthorizationRequestedServiceId);

        builder.Property(x => x.CodeValue)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.CodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.Description)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.PriorAuthorizationId);

        builder.HasIndex(x => x.CodeValue);
    }
}
