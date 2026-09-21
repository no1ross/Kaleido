using Kaleido.Samples.PriorAuth.Radiology.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Radiology.Data.Configurations;

internal sealed class PriorAuthorizationMemberConfiguration : IEntityTypeConfiguration<PriorAuthorizationMember>
{
    public void Configure(EntityTypeBuilder<PriorAuthorizationMember> builder)
    {
        builder.HasKey(x => x.PriorAuthorizationId);

        builder.Property(x => x.MemberNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PlanId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.PlanName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.LineOfBusiness)
            .HasConversion<string>();

        builder.HasIndex(x => x.MemberId);

        builder.HasIndex(x => x.MemberEnrollmentId);
    }
}
