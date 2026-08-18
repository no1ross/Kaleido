using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;

public static class ReferenceDataDbInitializer
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        ReferenceDataSeedModel data)
    {
        await using var scope =
            serviceProvider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider
                .GetRequiredService<ReferenceDataDbContext>();

        await dbContext.Database.EnsureCreatedAsync();

        dbContext.States.RemoveRange(dbContext.States);
        dbContext.ZipCodes.RemoveRange(dbContext.ZipCodes);
        dbContext.Plans.RemoveRange(dbContext.Plans);

        await dbContext.SaveChangesAsync();

        dbContext.States.AddRange(data.States);
        dbContext.ZipCodes.AddRange(data.ZipCodes);
        dbContext.Plans.AddRange(data.Plans);

        await dbContext.SaveChangesAsync();
    }
}
