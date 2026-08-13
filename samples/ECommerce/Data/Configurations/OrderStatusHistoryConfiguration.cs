using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(
        EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");

        builder.HasKey(
            orderStatusHistory => orderStatusHistory.OrderStatusHistoryId);

        builder.Property(
                orderStatusHistory => orderStatusHistory.FromStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(
                orderStatusHistory => orderStatusHistory.ToStatus)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(
                orderStatusHistory => orderStatusHistory.Reason)
            .HasMaxLength(500);

        builder.Property(
                orderStatusHistory => orderStatusHistory.ChangedUtc)
            .IsRequired();

        builder.HasOne(
                orderStatusHistory => orderStatusHistory.Order)
            .WithMany(
                order => order.StatusHistory)
            .HasForeignKey(
                orderStatusHistory => orderStatusHistory.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
            orderStatusHistory => orderStatusHistory.OrderId);

        builder.HasIndex(
            orderStatusHistory => orderStatusHistory.FromStatus);

        builder.HasIndex(
            orderStatusHistory => orderStatusHistory.ToStatus);

        builder.HasIndex(
            orderStatusHistory => orderStatusHistory.ChangedUtc);

        builder.HasIndex(
            orderStatusHistory => new
            {
                orderStatusHistory.OrderId,
                orderStatusHistory.ChangedUtc
            });
    }
}