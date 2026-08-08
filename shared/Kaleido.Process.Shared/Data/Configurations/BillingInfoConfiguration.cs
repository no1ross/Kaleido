using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class BillingInfoConfiguration : IEntityTypeConfiguration<BillingInfo>
{
    public void Configure(
        EntityTypeBuilder<BillingInfo> builder)
    {
        builder.ToTable("BillingInfos");

        builder.HasKey(
            billingInfo => billingInfo.BillingInfoId);

        builder.Property(
                billingInfo => billingInfo.CardholderName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                billingInfo => billingInfo.CardLastFourDigits)
            .HasMaxLength(4)
            .IsRequired();

        builder.Property(
                billingInfo => billingInfo.BillingAddress1)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                billingInfo => billingInfo.BillingAddress2)
            .HasMaxLength(200);

        builder.Property(
                billingInfo => billingInfo.City)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                billingInfo => billingInfo.State)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                billingInfo => billingInfo.PostalCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(
                billingInfo => billingInfo.Country)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasOne(
                billingInfo => billingInfo.Order)
            .WithOne(
                order => order.BillingInfo)
            .HasForeignKey<BillingInfo>(
                billingInfo => billingInfo.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
                billingInfo => billingInfo.OrderId)
            .IsUnique();

        builder.HasIndex(
            billingInfo => billingInfo.PostalCode);
    }
}
