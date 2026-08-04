namespace Kaleido.Process.Shared.Data;

public sealed class ShoppingCartItem
{
    public Guid ShoppingCartItemId { get; set; }

    public Guid ShoppingCartId { get; set; }

    public required string ItemId { get; set; }

    public required string Description { get; set; }

    public CartItemType ItemType { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset UpdatedOn { get; set; }

    public ShoppingCart ShoppingCart { get; set; } = null!;
}