namespace Kaleido.Samples.PriorAuth.Seeder;

internal sealed class SeedRunner(
    IReadOnlyList<IDomainSeeder> domainSeeders)
{
    public async Task RunAsync(
        IReadOnlyCollection<SupportedDomain> requestedDomains,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainSeeder in domainSeeders)
        {
            if (!requestedDomains.Contains(domainSeeder.Domain))
            {
                continue;
            }

            await domainSeeder.SeedAsync(cancellationToken);
        }
    }
}
