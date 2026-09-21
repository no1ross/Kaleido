using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Configurations;

internal sealed class PriorAuthorizationRequestedServiceConfiguration : IEntityTypeConfiguration<PriorAuthorizationRequestedService>
{
    public void Configure(EntityTypeBuilder<PriorAuthorizationRequestedService> builder)
    {
        builder.HasKey(x => x.PriorAuthorizationRequestedServiceId);

        builder.Property(x => x.UserEnteredCodeValue)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.UserEnteredCodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.ResolvedCodeValue)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.ResolvedCodeSystem)
            .HasConversion<string>();

        builder.Property(x => x.Description)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.PriorAuthorizationId);

        builder.HasIndex(x => x.UserEnteredCodeValue);

        builder.HasIndex(x => x.ResolvedCodeValue);
    }
}
