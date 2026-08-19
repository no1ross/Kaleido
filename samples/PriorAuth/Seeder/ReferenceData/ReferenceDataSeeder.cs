using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.ReferenceData;

internal sealed class ReferenceDataSeeder(
    ServiceProjectContextFactory projectContextFactory,
    JsonAssetLoader jsonAssetLoader)
    : IDomainSeeder
{
    public SupportedDomain Domain => SupportedDomain.ReferenceData;

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var provider =
            projectContextFactory.CreateSqliteDbContextProvider<ReferenceDataDbContext>(
                serviceProjectName: "ReferenceData",
                connectionStringName: "ReferenceData",
                fallbackConnectionString: "Data Source=data/referencedata.db");

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var states =
            jsonAssetLoader.Load<List<State>>(
                Path.Combine(
                    "referencedata",
                    "states.json"));

        var zipCodes =
            jsonAssetLoader.Load<List<ZipCode>>(
                Path.Combine(
                    "referencedata",
                    "zipcodes.json"));

        var plans =
            jsonAssetLoader.Load<List<Plan>>(
                Path.Combine(
                    "referencedata",
                    "plans.json"),
                jsonAssetLoader.CreateEnumJsonOptions());

        dbContext.States.AddRange(states);
        dbContext.ZipCodes.AddRange(zipCodes);
        dbContext.Plans.AddRange(plans);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
