using Kaleido.Samples.PriorAuth.Member.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.Member.Data.Configurations;

internal sealed class MemberSnapshotConfiguration : IEntityTypeConfiguration<MemberSnapshot>
{
    public void Configure(EntityTypeBuilder<MemberSnapshot> builder)
    {
        builder.HasKey(x => x.MemberSnapshotId);

        builder.HasIndex(x => x.MemberId);

        builder.HasIndex(x => x.MemberEnrollmentId);

        builder.Property(x => x.SnapshotJson)
            .IsRequired();
    }
}
