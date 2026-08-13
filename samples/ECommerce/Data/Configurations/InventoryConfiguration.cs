using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(
        EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("Inventories");

        builder.HasKey(
            inventory => inventory.InventoryId);

        builder.Property(
                inventory => inventory.AvailableQuantity)
            .IsRequired();

        builder.Property(
                inventory => inventory.ReorderThreshold)
            .IsRequired();

        builder.Property(
                inventory => inventory.UpdatedUtc)
            .IsRequired();

        builder.HasOne(
                inventory => inventory.Product)
            .WithOne(
                product => product.Inventory)
            .HasForeignKey<Inventory>(
                inventory => inventory.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
                inventory => inventory.ProductId)
            .IsUnique();

        builder.HasIndex(
            inventory => inventory.AvailableQuantity);

        builder.HasIndex(
            inventory => inventory.ReorderThreshold);

        builder.HasIndex(
            inventory => inventory.UpdatedUtc);
    }
}
