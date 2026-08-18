namespace Kaleido.Samples.PriorAuth.Seeder;

internal interface IDomainSeeder
{
    SupportedDomain Domain { get; }

    Task SeedAsync(
        CancellationToken cancellationToken = default);
}
