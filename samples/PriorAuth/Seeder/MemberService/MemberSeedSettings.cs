namespace Kaleido.Samples.PriorAuth.Seeder.MemberService;

internal sealed class MemberSeedSettings
{
    public int MemberCount { get; set; }

    public int AdditionalEnrollmentModulo { get; set; }

    public int TermedEnrollmentModulo { get; set; }

    public int SecondaryAddressModulo { get; set; }

    public int MiddleNameModulo { get; set; }

    public int AddressLine2Modulo { get; set; }

    public int PendingEnrollmentModulo { get; set; }

    public int CobraEnrollmentModulo { get; set; }

    public DateTimeOffset BaseCreatedUtc { get; set; }

    public DateOnly BaseEffectiveDate { get; set; }

    public int MinimumAgeYears { get; set; }

    public int AgeRangeYears { get; set; }

    public List<string> AllowedStates { get; set; } = [];
}
