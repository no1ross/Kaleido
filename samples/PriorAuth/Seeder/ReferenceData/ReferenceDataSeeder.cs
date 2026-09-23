using Kaleido.Samples.PriorAuth.ReferenceData.Data;
using Kaleido.Samples.PriorAuth.ReferenceData.Data.Entities;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.ReferenceData;

internal sealed class ReferenceDataSeeder(
    ServiceProjectContextFactory projectContextFactory)
    : IDomainSeeder
{
    public SupportedDomain Domain => SupportedDomain.ReferenceData;

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var provider =
            projectContextFactory.CreateSqliteDbContextProvider<ReferenceDataDbContext>(
                connectionString: "Data Source=referencedata.db");

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<ReferenceDataDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var states =
            JsonAssetLoader.Load<List<State>>(
                Path.Combine(
                    "referencedata",
                    "states.json"));

        var zipCodes =
            JsonAssetLoader.Load<List<ZipCode>>(
                Path.Combine(
                    "referencedata",
                    "zipcodes.json"));

        var plans =
            JsonAssetLoader.Load<List<Plan>>(
                Path.Combine(
                    "referencedata",
                    "plans.json"),
                JsonAssetLoader.CreateEnumJsonOptions());

        var networks =
            JsonAssetLoader.Load<List<Network>>(
                Path.Combine(
                    "referencedata",
                    "networks.json"));

        var planNetworks =
            JsonAssetLoader.Load<List<PlanNetwork>>(
                Path.Combine(
                    "referencedata",
                    "plan-networks.json"));

        dbContext.States.AddRange(states);
        dbContext.ZipCodes.AddRange(zipCodes);
        dbContext.Plans.AddRange(plans);
        dbContext.Networks.AddRange(networks);

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.PlanNetworks.AddRange(planNetworks);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
