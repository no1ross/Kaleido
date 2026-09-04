using Kaleido.Samples.PriorAuth.Member.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Member.Data.Configurations;

internal sealed class MemberConfiguration : IEntityTypeConfiguration<MemberInfo>
{
    public void Configure(EntityTypeBuilder<MemberInfo> builder)
    {
        builder.HasKey(x => x.MemberId);

        builder.Property(x => x.MemberNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.MiddleName)
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.EmailAddress)
            .HasMaxLength(200);

        builder.Property(x => x.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(x => x.Gender)
            .HasConversion<string>();

        builder.HasIndex(x => x.MemberNumber)
            .IsUnique();

        builder.HasMany(x => x.Addresses)
            .WithOne(x => x.Member)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Enrollments)
            .WithOne(x => x.Member)
            .HasForeignKey(x => x.MemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
