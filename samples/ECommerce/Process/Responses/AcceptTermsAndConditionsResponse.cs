namespace Kaleido.Samples.ECommerce.Process.Responses;

public sealed record AcceptTermsAndConditionsResponse
{
    public required bool Accepted { get; init; }

    public required string TermsVersion { get; init; }

    public required DateTimeOffset AcceptedOn { get; init; }
}