using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.Configuration;

internal sealed class ConfigurationSeeder(
    ServiceProjectContextFactory projectContextFactory,
    JsonAssetLoader jsonAssetLoader)
    : IDomainSeeder
{
    public SupportedDomain Domain => SupportedDomain.Configuration;

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var provider =
            projectContextFactory.CreateSqliteDbContextProvider<ConfigurationDbContext>(
                connectionString: "Data Source=Configuration/data/configuration.db");

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var assets = LoadAssets();

        dbContext.ProcedureModalityRules.AddRange(assets.ProcedureModalityRules);
        dbContext.MriProcedureCodeRules.AddRange(assets.MriProcedureCodeRules);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private ConfigurationSeedAssets LoadAssets()
    {
        const string basePath = "configuration";
        var enumOptions = jsonAssetLoader.CreateEnumJsonOptions();

        return new ConfigurationSeedAssets
        {
            ProcedureModalityRules = jsonAssetLoader.Load<List<Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities.ProcedureModalityRule>>(Path.Combine(basePath, "procedure-modality-rules.json"), enumOptions),
            MriProcedureCodeRules = jsonAssetLoader.Load<List<Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities.MriProcedureCodeRule>>(Path.Combine(basePath, "mri-procedure-code-rules.json"), enumOptions)
        };
    }
}
