namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class ProductCategoryAssignment
{
    public Guid ProductId { get; set; }

    public Guid ProductCategoryId { get; set; }

    public Product Product { get; set; } = null!;

    public ProductCategory Category { get; set; } = null!;
}