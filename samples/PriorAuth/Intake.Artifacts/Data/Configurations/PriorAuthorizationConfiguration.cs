using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Configurations;

internal sealed class PriorAuthorizationConfiguration : IEntityTypeConfiguration<PriorAuthorization>
{
    public void Configure(EntityTypeBuilder<PriorAuthorization> builder)
    {
        builder.HasKey(x => x.PriorAuthorizationId);

        builder.Property(x => x.Status)
            .HasConversion<string>();

        builder.HasIndex(x => x.ProcessId)
            .IsUnique();

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.CreatedUtc);

        builder.HasOne(x => x.Member)
            .WithOne(x => x.PriorAuthorization)
            .HasForeignKey<PriorAuthorizationMember>(x => x.PriorAuthorizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RequestingProvider)
            .WithOne(x => x.PriorAuthorization)
            .HasForeignKey<PriorAuthorizationRequestingProvider>(x => x.PriorAuthorizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.RequestedServices)
            .WithOne(x => x.PriorAuthorization)
            .HasForeignKey(x => x.PriorAuthorizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
