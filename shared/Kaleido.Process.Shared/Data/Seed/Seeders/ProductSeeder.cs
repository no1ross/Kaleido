using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class ProductSeeder
{
    public void Seed(
        ECommerceDbContext dbContext,
        IReadOnlyDictionary<string, SupplierDefinition> suppliers)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        ArgumentNullException.ThrowIfNull(
            suppliers);

        var supplierLookup =
            dbContext.Suppliers
                .ToDictionary(
                    x => x.Name,
                    StringComparer.OrdinalIgnoreCase);

        var products =
            new List<Product>();

        foreach (var supplierDefinition in suppliers)
        {
            var supplierName =
                supplierDefinition.Key;

            var supplier =
                supplierLookup[supplierName];

            foreach (var familyDefinition in supplierDefinition.Value.Families)
            {
                var familyName =
                    familyDefinition.Key;

                foreach (var model in familyDefinition.Value.Models)
                {
                    products.Add(
                        new Product
                        {
                            ProductId =
                                Guid.NewGuid(),

                            SupplierId =
                                supplier.SupplierId,

                            Name =
                                $"{supplierName} {familyName} {model}",

                            Sku =
                                CreateSku(
                                    supplierName,
                                    familyName,
                                    model),

                            Description =
                                CreateDescription(
                                    supplierName,
                                    familyName,
                                    model),

                            Price =
                                GeneratePrice(),

                            Rating =
                                GenerateRating(),

                            ReviewCount =
                                GenerateReviewCount(),

                            CreatedUtc =
                                GenerateCreatedDate(),

                            ReleasedUtc =
                                GenerateReleaseDate(),

                            IsActive =
                                true
                        });
                }
            }
        }

        dbContext.Products.AddRange(
            products);

        dbContext.SaveChanges();
    }

    private static string CreateSku(
        string supplier,
        string family,
        string model)
    {
        return
            $"{supplier[..Math.Min(3, supplier.Length)].ToUpperInvariant()}-" +
            $"{family[..Math.Min(3, family.Length)].ToUpperInvariant()}-" +
            model.ToUpperInvariant();
    }

    private static string CreateDescription(
        string supplier,
        string family,
        string model)
    {
        return
            $"{supplier} {family} {model} is a product offered by {supplier}.";
    }

    private static decimal GeneratePrice()
        => Random.Shared.Next(
            10,
            5000);

    private static double GenerateRating()
        => Math.Round(
            Random.Shared.NextDouble() * 2 + 3,
            1);

    private static int GenerateReviewCount()
        => Random.Shared.Next(
            0,
            5000);

    private static DateTime GenerateCreatedDate()
        => DateTime.UtcNow.AddDays(
            -Random.Shared.Next(
                0,
                3650));

    private static DateTime GenerateReleaseDate()
        => DateTime.UtcNow.AddDays(
            -Random.Shared.Next(
                0,
                3650));
}