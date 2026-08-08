using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(
        EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(
            customer => customer.CustomerId);

        builder.Property(
                customer => customer.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                customer => customer.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                customer => customer.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(
                customer => customer.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(
                customer => customer.IsActive)
            .IsRequired();

        builder.Property(
                customer => customer.CreatedUtc)
            .IsRequired();

        builder.HasIndex(
                customer => customer.Email)
            .IsUnique();

        builder.HasIndex(
            customer => new
            {
                customer.LastName,
                customer.FirstName
            });

        builder.HasIndex(
            customer => customer.IsActive);

        builder.HasMany(
                customer => customer.ShoppingCarts)
            .WithOne(
                shoppingCart => shoppingCart.Customer)
            .HasForeignKey(
                shoppingCart => shoppingCart.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(
                customer => customer.Orders)
            .WithOne(
                order => order.Customer)
            .HasForeignKey(
                order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
