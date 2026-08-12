using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class ProductCategoryAssignmentSeeder
{
    public void Seed(
        ECommerceDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        var leafCategories =
            dbContext.ProductCategories
                .Where(x =>
                    !dbContext.ProductCategories.Any(
                        y => y.ParentProductCategoryId ==
                             x.ProductCategoryId))
                .ToList();

        var products =
            dbContext.Products
                .ToList();

        var productFamilies =
            products
                .GroupBy(x => new
                {
                    x.SupplierId,
                    x.FamilyName
                })
                .ToList();

        var assignments =
            new List<ProductCategoryAssignment>();

        foreach (var family in productFamilies)
        {
            var categories =
                leafCategories
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(Random.Shared.Next(2, 5))
                    .ToList();

            foreach (var product in family)
            {
                foreach (var category in categories)
                {
                    assignments.Add(
                        new ProductCategoryAssignment
                        {
                            ProductId =
                                product.ProductId,

                            ProductCategoryId =
                                category.ProductCategoryId
                        });
                }
            }
        }

        dbContext.ProductCategoryAssignments
            .AddRange(assignments);

        dbContext.SaveChanges();
    }
}