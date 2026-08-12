using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(
        EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(
            product => product.ProductId);

        builder.Property(
                product => product.Sku)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(
                product => product.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                product => product.FamilyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                product => product.ModelName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                product => product.Description)
            .HasMaxLength(1000);

        builder.Property(
                product => product.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(
                product => product.IsActive)
            .IsRequired();

        builder.Property(
                product => product.CreatedUtc)
            .IsRequired();

        builder.HasMany(
                product => product.CategoryAssignments)
            .WithOne(
                assignment => assignment.Product)
            .HasForeignKey(
                assignment => assignment.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(
                product => product.Supplier)
            .WithMany(
                supplier => supplier.Products)
            .HasForeignKey(
                product => product.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(
                product => product.Inventory)
            .WithOne(
                inventory => inventory.Product)
            .HasForeignKey<Inventory>(
                inventory => inventory.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
                product => product.Sku)
            .IsUnique();

        builder.HasIndex(
            product => product.Name);

        builder.HasIndex(
            product => product.SupplierId);

        builder.HasIndex(
            product => product.Price);

        builder.HasIndex(
            product => product.IsActive);

        builder.HasIndex(
            product => product.CreatedUtc);
    }
}
