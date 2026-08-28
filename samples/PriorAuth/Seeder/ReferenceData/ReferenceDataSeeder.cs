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
                connectionString: "Data Source=referencedata.db");

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

        var networks =
            jsonAssetLoader.Load<List<Network>>(
                Path.Combine(
                    "referencedata",
                    "networks.json"));

        var planNetworks =
            jsonAssetLoader.Load<List<PlanNetwork>>(
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
