using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(
        EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(
            supplier => supplier.SupplierId);

        builder.Property(
                supplier => supplier.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(
                supplier => supplier.ContactName)
            .HasMaxLength(200);

        builder.Property(
                supplier => supplier.Email)
            .HasMaxLength(256);

        builder.Property(
                supplier => supplier.IsPreferred)
            .IsRequired();

        builder.Property(
                supplier => supplier.IsActive)
            .IsRequired();

        builder.HasIndex(
                supplier => supplier.Name)
            .IsUnique();

        builder.HasIndex(
            supplier => supplier.IsPreferred);

        builder.HasIndex(
            supplier => supplier.IsActive);

        builder.HasMany(
                supplier => supplier.Products)
            .WithOne(
                product => product.Supplier)
            .HasForeignKey(
                product => product.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
