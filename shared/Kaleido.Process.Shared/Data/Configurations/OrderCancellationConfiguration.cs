using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class OrderCancellationConfiguration : IEntityTypeConfiguration<OrderCancellation>
{
    public void Configure(
        EntityTypeBuilder<OrderCancellation> builder)
    {
        builder.ToTable("OrderCancellations");

        builder.HasKey(
            orderCancellation => orderCancellation.OrderCancellationId);

        builder.Property(
                orderCancellation => orderCancellation.Reason)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(
                orderCancellation => orderCancellation.CancelledUtc)
            .IsRequired();

        builder.HasOne(
                orderCancellation => orderCancellation.Order)
            .WithOne(order => order.Cancellation)
            .HasForeignKey<OrderCancellation>(
                orderCancellation => orderCancellation.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
                orderCancellation => orderCancellation.OrderId)
            .IsUnique();

        builder.HasIndex(
            orderCancellation => orderCancellation.CancelledUtc);
    }
}
