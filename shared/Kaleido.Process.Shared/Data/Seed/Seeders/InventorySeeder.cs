using Kaleido.Samples.ECommerce.Data.Entities;

namespace Kaleido.Samples.ECommerce.Data.Seed.Seeders;

internal sealed class InventorySeeder
{
    public void Seed(
        ECommerceDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(
            dbContext);

        var inventories =
            dbContext.Products
                .Select(
                    product => new Inventory
                    {
                        InventoryId =
                            Guid.NewGuid(),

                        ProductId =
                            product.ProductId,

                        AvailableQuantity =
                            GenerateQuantity(),

                        ReorderThreshold =
                            GenerateReorderThreshold(),

                        UpdatedUtc =
                            DateTime.UtcNow
                    })
                .ToList();

        dbContext.Inventories.AddRange(
            inventories);

        dbContext.SaveChanges();
    }

    private static int GenerateQuantity()
    {
        var bucket =
            Random.Shared.Next(
                1,
                101);

        return bucket switch
        {
            <= 10 => 0,

            <= 25 => Random.Shared.Next(1, 10),

            <= 75 => Random.Shared.Next(10, 100),

            _ => Random.Shared.Next(100, 500)
        };
    }

    private static int GenerateReorderThreshold()
    {
        return Random.Shared.Next(
            10,
            100);
    }
}