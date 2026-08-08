namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class Inventory
{
    public Guid InventoryId { get; set; }

    public Guid ProductId { get; set; }

    public int AvailableQuantity { get; set; }

    public int ReorderThreshold { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public Product Product { get; set; } = null!;
}