using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class ProductCategoryAssignmentSeeder
{
    public void Seed(
        ECommerceDbContext dbContext,
        IReadOnlyDictionary<string, SupplierDefinition> suppliers)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        ArgumentNullException.ThrowIfNull(
            suppliers);

        var categories =
            dbContext.ProductCategories
                .ToDictionary(
                    x => x.Path,
                    StringComparer.OrdinalIgnoreCase);

        var products =
            dbContext.Products
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        var assignments =
            new List<ProductCategoryAssignment>();

        foreach (var supplier in suppliers)
        {
            foreach (var family in supplier.Value.Families)
            {
                var category =
                    categories[
                        family.Value.PrimaryCategory];

                foreach (var model in family.Value.Models)
                {
                    var productName =
                        $"{supplier.Key} {family.Key} {model}";

                    var product =
                        products[productName];

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