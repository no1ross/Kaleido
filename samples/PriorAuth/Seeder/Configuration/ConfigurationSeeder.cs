using Kaleido.Samples.PriorAuth.Configuration.Data;
using Kaleido.Samples.PriorAuth.Configuration.Data.Entities;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.Configuration;

internal sealed class ConfigurationSeeder(
    ServiceProjectContextFactory projectContextFactory)
    : IDomainSeeder
{
    public SupportedDomain Domain => SupportedDomain.Configuration;

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var provider =
            projectContextFactory.CreateSqliteDbContextProvider<ConfigurationDbContext>(
                connectionString: "Data Source=configuration.db");

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var assets = LoadAssets();

        dbContext.ProcedureModalityRules.AddRange(assets.ProcedureModalityRules);
        dbContext.ProductCodeMappings.AddRange(assets.ProductCodeMappings);
        dbContext.MriProcedureCodeRules.AddRange(assets.MriProcedureCodeRules);
        dbContext.QuestionnaireDefinitions.AddRange(assets.QuestionnaireDefinitions);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ConfigurationSeedAssets LoadAssets()
    {
        const string basePath = "configuration";
        var enumOptions = JsonAssetLoader.CreateEnumJsonOptions();

        return new ConfigurationSeedAssets
        {
            ProcedureModalityRules = JsonAssetLoader.Load<List<ProcedureModalityRule>>(Path.Combine(basePath, "procedure-modality-rules.json"), enumOptions),
            ProductCodeMappings = JsonAssetLoader.Load<List<ProductCodeMapping>>(Path.Combine(basePath, "product-code-mappings.json"), enumOptions),
            MriProcedureCodeRules = JsonAssetLoader.Load<List<MriProcedureCodeRule>>(Path.Combine(basePath, "mri-procedure-code-rules.json"), enumOptions),
            QuestionnaireDefinitions = JsonAssetLoader.Load<List<QuestionnaireDefinition>>(Path.Combine(basePath, "questionnaire-definitions.json"), enumOptions)
        };
    }
}
