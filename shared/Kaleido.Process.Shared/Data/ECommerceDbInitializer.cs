using Kaleido.Samples.ECommerce.Data.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.ECommerce.Data;

public static class ECommerceDbInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider)
    {
        await using var scope =
            serviceProvider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ECommerceDbContext>();

        Console.WriteLine("Before Migrate");

        await dbContext.Database.MigrateAsync();

        Console.WriteLine("After Migrate");

        var recreateDatabase = true;

        if (recreateDatabase)
        {
            await dbContext.Database.EnsureDeletedAsync();

            await dbContext.Database.EnsureCreatedAsync();
        }
        else
        {
            await dbContext.Database.MigrateAsync();
        }

        var seeder = new ECommerceSeeder(dbContext);

        seeder.Seed();
    }
}