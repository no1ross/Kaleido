namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class ShoppingCart
{
    public Guid ShoppingCartId { get; set; }

    public Guid CustomerId { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<ShoppingCartItem> Items { get; set; }
        = new List<ShoppingCartItem>();
}