using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(
        EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(
            order => order.OrderId);

        builder.Property(
                order => order.OrderNumber)
            .HasMaxLength(50);

        builder.Property(
                order => order.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(
                order => order.CreatedUtc)
            .IsRequired();

        builder.Property(
            order => order.SubmittedUtc);

        builder.Property(
            order => order.CancelledUtc);

        builder.HasOne(
                order => order.Customer)
            .WithMany(
                customer => customer.Orders)
            .HasForeignKey(
                order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(
                order => order.ShoppingCart)
            .WithMany()
            .HasForeignKey(
                order => order.ShoppingCartId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(
                order => order.BillingInfo)
            .WithOne(
                billingInfo => billingInfo.Order)
            .HasForeignKey<BillingInfo>(
                billingInfo => billingInfo.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(
                order => order.Items)
            .WithOne(
                orderItem => orderItem.Order)
            .HasForeignKey(
                orderItem => orderItem.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(
                order => order.StatusHistory)
            .WithOne(
                orderStatusHistory => orderStatusHistory.Order)
            .HasForeignKey(
                orderStatusHistory => orderStatusHistory.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
                order => order.OrderNumber)
            .IsUnique();

        builder.HasIndex(
            order => order.CustomerId);

        builder.HasIndex(
            order => order.ShoppingCartId);

        builder.HasIndex(
            order => order.ProcessId);

        builder.HasIndex(
            order => order.Status);

        builder.HasIndex(
            order => order.CreatedUtc);

        builder.HasIndex(
            order => order.SubmittedUtc);

        builder.HasIndex(
            order => order.CancelledUtc);

        builder.HasIndex(
            order => new
            {
                order.CustomerId,
                order.Status
            });
    }
}
