namespace Kaleido.Process.Shared.Responses;

public sealed record SubmitBillingResponse
{
    public required string BillingId { get; init; }

    public required PaymentMethodType PaymentMethod { get; init; }

    public required bool Accepted { get; init; }
    public decimal? AuthorizedAmount { get; internal set; }
    public string[] ValidationWarnings { get; internal set; }
}