using Kaleido.Samples.ECommerce.Data.Seed.Seeders;
using System.Text.Json;

namespace Kaleido.Samples.ECommerce.Data.Seed;

internal sealed class ECommerceSeeder
{
    private readonly ECommerceDbContext _dbContext;

    public ECommerceSeeder(
        ECommerceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Seed()
    {
        var taxonomy =
            LoadJson<TaxonomyDefinition>(
                "taxonomy.json");

        var suppliers =
            LoadJson<Dictionary<string, SupplierDefinition>>(
                "suppliers.json");

        var customers =
            LoadJson<List<CustomerDefinition>>(
                "customers.json");

        //var settings =
        //    LoadJson<SeedSettings>(
        //        "seedsettings.json");

        new TaxonomySeeder()
            .Seed(
                _dbContext,
                taxonomy);

        new SupplierSeeder()
            .Seed(
                _dbContext,
                suppliers);

        new CustomerSeeder()
            .Seed(
                _dbContext,
                customers);

        new ProductSeeder()
            .Seed(
                _dbContext,
                suppliers);

        new ProductCategoryAssignmentSeeder()
            .Seed(
                _dbContext,
                suppliers);

        new InventorySeeder()
            .Seed(
                _dbContext);
    }

    private static T LoadJson<T>(
        string fileName)
    {
        try
        {
            var path =
                Path.Combine(
                    AppContext.BaseDirectory,
                    "data/assets",
                    fileName);

                        var json =
                            File.ReadAllText(path);

                        return JsonSerializer.Deserialize<T>(
                                   json,
                                   new JsonSerializerOptions
                                   {
                                       PropertyNameCaseInsensitive = true
                                   })
                               ?? throw new InvalidOperationException(
                                   $"Failed to deserialize '{fileName}'.");
        }
        catch (Exception)
        {
            throw;
        }
    }

}