using System.ComponentModel.DataAnnotations.Schema;

namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class ShoppingCart
{
    public Guid ShoppingCartId { get; set; }

    public Guid? CustomerId { get; set; }

    public Guid ProcessId { get; init; }

    public bool IsActive { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<ShoppingCartItem> Items { get; set; }
        = new List<ShoppingCartItem>();

    [NotMapped]
    public int TotalItems => Items.Sum(i => i.Quantity);

    [NotMapped]
    public decimal TotalPrice => Items.Sum(i => i.Quantity * i.UnitPrice);
}