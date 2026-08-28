using Kaleido.Samples.PriorAuth.Seeder;
using Kaleido.Samples.PriorAuth.Seeder.CodeSet;
using Kaleido.Samples.PriorAuth.Seeder.Configuration;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Kaleido.Samples.PriorAuth.Seeder.MemberService;
using Kaleido.Samples.PriorAuth.Seeder.ProviderSearch;
using Kaleido.Samples.PriorAuth.Seeder.ReferenceData;

var rootConfiguration =
    SeedConfiguration.CreateRootConfiguration();

var seedSettings =
    SeedConfiguration.ResolveSettings(
        rootConfiguration);

var requestedDomains =
    SeedConfiguration.ResolveRequestedDomains(
        args,
        seedSettings);

var projectContextFactory =
    new ServiceProjectContextFactory(
        dataRoot: Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                seedSettings.DataRoot)));

var jsonAssetLoader =
    new JsonAssetLoader();

var runner =
    new SeedRunner(
        [
            new ReferenceDataSeeder(
                projectContextFactory,
                jsonAssetLoader),

            new CodeSetSeeder(
                projectContextFactory,
                jsonAssetLoader),

            new ConfigurationSeeder(
                projectContextFactory,
                jsonAssetLoader),

            new ProviderSearchSeeder(
                projectContextFactory,
                jsonAssetLoader),

            new MemberServiceSeeder(
                projectContextFactory,
                jsonAssetLoader)
        ]);

await runner.RunAsync(requestedDomains);
