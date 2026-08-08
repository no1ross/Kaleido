namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class ProductCategory
{
    public Guid ProductCategoryId { get; set; }

    public Guid? ParentProductCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public ProductCategory? ParentCategory { get; set; }

    public ICollection<ProductCategory> ChildCategories { get; set; }
        = new List<ProductCategory>();

    public ICollection<ProductCategoryAssignment> ProductAssignments { get; set; }
        = new List<ProductCategoryAssignment>();
}