namespace Kaleido.Samples.PriorAuth.Seeder.MemberService;

internal sealed class MemberSeedAssets
{
    public required List<string> FirstNames { get; init; }

    public required List<string> LastNames { get; init; }

    public required List<string> StreetNames { get; init; }

    public required List<string> StreetSuffixes { get; init; }

    public required List<string> AddressLine2Patterns { get; init; }

    public required MemberSeedSettings Settings { get; init; }
}
