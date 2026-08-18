using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Configurations;

internal sealed class MemberAddressConfiguration : IEntityTypeConfiguration<MemberAddress>
{
    public void Configure(EntityTypeBuilder<MemberAddress> builder)
    {
        builder.HasKey(x => x.MemberAddressId);

        builder.Property(x => x.AddressLine1)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.AddressLine2)
            .HasMaxLength(200);

        builder.Property(x => x.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.State)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.PostalCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.MemberId);

        builder.HasIndex(x => x.State);
    }
}
