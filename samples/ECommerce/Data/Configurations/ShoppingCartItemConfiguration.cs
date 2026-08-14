using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class ShoppingCartItemConfiguration : IEntityTypeConfiguration<ShoppingCartItem>
{
    public void Configure(
        EntityTypeBuilder<ShoppingCartItem> builder)
    {
        builder.ToTable("ShoppingCartItems");

        builder.HasKey(
            shoppingCartItem => shoppingCartItem.ShoppingCartItemId);

        builder.Property(
                shoppingCartItem => shoppingCartItem.Quantity)
            .IsRequired();

        builder.Property(
                shoppingCartItem => shoppingCartItem.UnitPrice)
            .IsRequired();

        builder.HasOne(
                shoppingCartItem => shoppingCartItem.ShoppingCart)
            .WithMany(
                shoppingCart => shoppingCart.Items)
            .HasForeignKey(
                shoppingCartItem => shoppingCartItem.ShoppingCartId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(
                shoppingCartItem => shoppingCartItem.Product)
            .WithMany()
            .HasForeignKey(
                shoppingCartItem => shoppingCartItem.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(
                shoppingCartItem => new
                {
                    shoppingCartItem.ShoppingCartId,
                    shoppingCartItem.ProductId
                })
            .IsUnique();

        builder.HasIndex(
            shoppingCartItem => shoppingCartItem.ProductId);
    }
}
