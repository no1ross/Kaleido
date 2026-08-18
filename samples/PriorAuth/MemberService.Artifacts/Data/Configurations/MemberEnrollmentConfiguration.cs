using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Configurations;

internal sealed class MemberEnrollmentConfiguration : IEntityTypeConfiguration<MemberEnrollment>
{
    public void Configure(EntityTypeBuilder<MemberEnrollment> builder)
    {
        builder.HasKey(x => x.MemberEnrollmentId);

        builder.Property(x => x.PlanId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.PlanName)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(x => x.MemberId);

        builder.HasIndex(x => x.MemberAddressId);

        builder.HasIndex(x => x.LineOfBusiness);

        builder.HasOne(x => x.Address)
            .WithMany(x => x.Enrollments)
            .HasForeignKey(x => x.MemberAddressId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
