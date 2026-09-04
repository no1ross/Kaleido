using Kaleido.Samples.PriorAuth.Intake.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Configurations;

internal sealed class IntakeSessionMemberConfiguration : IEntityTypeConfiguration<IntakeSessionMember>
{
    public void Configure(EntityTypeBuilder<IntakeSessionMember> builder)
    {
        builder.HasKey(x => x.IntakeSessionId);

        builder.Property(x => x.MemberNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.MemberId);

        builder.HasIndex(x => x.MemberEnrollmentId);
    }
}
