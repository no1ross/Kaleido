using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class ShoppingCartConfiguration : IEntityTypeConfiguration<ShoppingCart>
{
    public void Configure(
        EntityTypeBuilder<ShoppingCart> builder)
    {
        builder.ToTable("ShoppingCarts");

        builder.HasKey(
            shoppingCart => shoppingCart.ShoppingCartId);

        builder.Property(
                shoppingCart => shoppingCart.IsActive)
            .IsRequired();

        builder.Property(
                shoppingCart => shoppingCart.CreatedUtc)
            .IsRequired();

        builder.Property(
                shoppingCart => shoppingCart.UpdatedUtc)
            .IsRequired();

        builder.HasOne(
                shoppingCart => shoppingCart.Customer)
            .WithMany(
                customer => customer.ShoppingCarts)
            .HasForeignKey(
                shoppingCart => shoppingCart.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(
                shoppingCart => shoppingCart.Items)
            .WithOne(
                shoppingCartItem => shoppingCartItem.ShoppingCart)
            .HasForeignKey(
                shoppingCartItem => shoppingCartItem.ShoppingCartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
            shoppingCart => shoppingCart.CustomerId);

        builder.HasIndex(
            shoppingCart => shoppingCart.ParticipantProcessId)
            .IsUnique()
            .HasFilter("[ParticipantProcessId] IS NOT NULL");

        builder.HasIndex(
            shoppingCart => new
            {
                shoppingCart.CustomerId,
                shoppingCart.IsActive
            });

        builder.HasIndex(
            shoppingCart => shoppingCart.CreatedUtc);

        builder.HasIndex(
            shoppingCart => shoppingCart.UpdatedUtc);
    }
}
