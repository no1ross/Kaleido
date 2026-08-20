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

        dbContext.PlanNetworks.RemoveRange(dbContext.PlanNetworks);
        dbContext.Plans.RemoveRange(dbContext.Plans);
        dbContext.ZipCodes.RemoveRange(dbContext.ZipCodes);
        dbContext.Networks.RemoveRange(dbContext.Networks);
        dbContext.States.RemoveRange(dbContext.States);

        await dbContext.SaveChangesAsync();

        dbContext.States.AddRange(data.States);
        dbContext.ZipCodes.AddRange(data.ZipCodes);
        dbContext.Plans.AddRange(data.Plans);
        dbContext.Networks.AddRange(data.Networks);

        await dbContext.SaveChangesAsync();

        dbContext.PlanNetworks.AddRange(data.PlanNetworks);

        await dbContext.SaveChangesAsync();
    }
}
