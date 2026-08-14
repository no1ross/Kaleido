namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class ShoppingCartItem
{
    public Guid ShoppingCartItemId { get; set; }

    public Guid ShoppingCartId { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public ShoppingCart ShoppingCart { get; set; } = null!;

    public Product Product { get; set; } = null!;
}