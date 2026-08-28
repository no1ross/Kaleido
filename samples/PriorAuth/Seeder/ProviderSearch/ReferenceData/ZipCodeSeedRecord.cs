namespace Kaleido.Samples.PriorAuth.Seeder.ProviderSearch.ReferenceData;

internal sealed class ZipCodeSeedRecord
{
    public required string PostalCode { get; init; }

    public required string StateCode { get; init; }

    public required string City { get; init; }

    public bool IsActive { get; init; }
}
