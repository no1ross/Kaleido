using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class TaxonomySeeder
{
    public void Seed(
        ECommerceDbContext dbContext,
        TaxonomyDefinition definition)
    {
        var categories =
            new List<ProductCategory>();

        foreach (var category in definition)
        {
            CreateCategory(
                categoryName: category.Key,
                definition: category.Value,
                parentCategoryId: null,
                parentPath: null,
                categories: categories);
        }

        dbContext.ProductCategories.AddRange(
            categories);

        dbContext.SaveChanges();
    }

    private static void CreateCategory(
        string categoryName,
        TaxonomyNode definition,
        Guid? parentCategoryId,
        string? parentPath,
        ICollection<ProductCategory> categories)
    {
        var categoryId =
            Guid.NewGuid();

        var categoryPath =
            string.IsNullOrWhiteSpace(parentPath)
                ? categoryName
                : $"{parentPath}/{categoryName}";

        categories.Add(
            new ProductCategory
            {
                ProductCategoryId =
                    categoryId,

                ParentProductCategoryId =
                    parentCategoryId,

                Name =
                    categoryName,

                Path =
                    categoryPath,

                Description =
                    $"Products classified under the {categoryName} category.",

                IsActive =
                    true
            });

        foreach (var child in definition)
        {
            CreateCategory(
                categoryName: child.Key,
                definition: child.Value,
                parentCategoryId: categoryId,
                parentPath: categoryPath,
                categories: categories);
        }
    }
}