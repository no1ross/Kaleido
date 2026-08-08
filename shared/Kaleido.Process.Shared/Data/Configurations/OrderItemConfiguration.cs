using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(
        EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems");

        builder.HasKey(
            orderItem => orderItem.OrderItemId);

        builder.Property(
                orderItem => orderItem.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                orderItem => orderItem.ProductSku)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(
                orderItem => orderItem.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(
                orderItem => orderItem.Quantity)
            .IsRequired();

        builder.HasOne(
                orderItem => orderItem.Order)
            .WithMany(
                order => order.Items)
            .HasForeignKey(
                orderItem => orderItem.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(
                orderItem => orderItem.Product)
            .WithMany()
            .HasForeignKey(
                orderItem => orderItem.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(
            orderItem => orderItem.OrderId);

        builder.HasIndex(
            orderItem => orderItem.ProductId);

        builder.HasIndex(
            orderItem => orderItem.ProductSku);
    }
}
