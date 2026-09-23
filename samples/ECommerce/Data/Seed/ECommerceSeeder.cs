using System.Text.Json;
using Kaleido.Samples.ECommerce.Data.Seed.Seeders;

namespace Kaleido.Samples.ECommerce.Data.Seed;

internal sealed class ECommerceSeeder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new()
        {
            PropertyNameCaseInsensitive = true
        };

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
            LoadJson<List<SupplierDefinition>>(
                "suppliers.json");

        var customers =
            LoadJson<List<CustomerDefinition>>(
                "customers.json");

        //var settings =
        //    LoadJson<SeedSettings>(
        //        "seedsettings.json");

        TaxonomySeeder.Seed(
            _dbContext,
            taxonomy);

        SupplierSeeder.Seed(
            _dbContext,
            suppliers);

        CustomerSeeder.Seed(
            _dbContext,
            customers);

        ProductSeeder.Seed(
            _dbContext,
            suppliers);

        ProductCategoryAssignmentSeeder.Seed(
            _dbContext);

        InventorySeeder.Seed(
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
                    "assets",
                    fileName);

            var json =
                File.ReadAllText(path);

            return JsonSerializer.Deserialize<T>(
                       json,
                       JsonOptions)
                   ?? throw new InvalidOperationException(
                       $"Failed to deserialize '{fileName}'.");
        }
        catch (Exception)
        {
            throw;
        }
    }

}