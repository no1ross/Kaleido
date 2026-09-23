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

var runner =
    new SeedRunner(
        [
            new ReferenceDataSeeder(
                projectContextFactory),

            new CodeSetSeeder(
                projectContextFactory),

            new ConfigurationSeeder(
                projectContextFactory),

            new ProviderSearchSeeder(
                projectContextFactory),

            new MemberServiceSeeder(
                projectContextFactory)
        ]);

await runner.RunAsync(requestedDomains);
