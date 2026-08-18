using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;

public static class ReferenceDataDbInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider)
    {
        await using var scope =
            serviceProvider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ReferenceDataDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        if (await dbContext.States.AnyAsync()
            || await dbContext.ZipCodes.AnyAsync()
            || await dbContext.Plans.AnyAsync())
        {
            return;
        }

        var data = ReferenceDataSeedData.Create();

        dbContext.States.AddRange(data.States);
        dbContext.ZipCodes.AddRange(data.ZipCodes);
        dbContext.Plans.AddRange(data.Plans);

        await dbContext.SaveChangesAsync();
    }
}
