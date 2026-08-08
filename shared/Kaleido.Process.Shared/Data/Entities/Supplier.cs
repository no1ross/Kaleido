namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class Supplier
{
    public Guid SupplierId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ContactName { get; set; }

    public string? Email { get; set; }

    public bool IsPreferred { get; set; }

    public bool IsActive { get; set; }

    public ICollection<Product> Products { get; set; }
        = new List<Product>();
}