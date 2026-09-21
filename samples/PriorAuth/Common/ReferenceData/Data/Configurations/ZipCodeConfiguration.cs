using Kaleido.Samples.PriorAuth.ReferenceData.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Data.Configurations;

internal sealed class ZipCodeConfiguration : IEntityTypeConfiguration<ZipCode>
{
    public void Configure(EntityTypeBuilder<ZipCode> builder)
    {
        builder.HasKey(x => x.PostalCode);

        builder.Property(x => x.PostalCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.StateCode)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x => x.StateCode);

        builder.HasIndex(x => new { x.StateCode, x.City });

        builder.HasOne(x => x.State)
            .WithMany(x => x.ZipCodes)
            .HasForeignKey(x => x.StateCode)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
