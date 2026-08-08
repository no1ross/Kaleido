namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class Product
{
    public Guid ProductId { get; set; }

    public Guid SupplierId { get; set; }

    public string Sku { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedUtc { get; set; }

    public ICollection<ProductCategoryAssignment> CategoryAssignments { get; set; }
        = new List<ProductCategoryAssignment>();

    public Supplier Supplier { get; set; } = null!;

    public Inventory Inventory { get; set; } = null!;

    public double Rating { get; set; }

    public int ReviewCount { get; set; }

    public DateTime ReleasedUtc { get; set; }
}