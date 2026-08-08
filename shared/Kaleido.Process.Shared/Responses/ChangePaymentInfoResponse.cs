using Kaleido.Samples.ECommerce;

namespace Kaleido.Samples.ECommerce.Responses;

public sealed record ChangePaymentInfoResponse
{
    public required bool Updated { get; init; }

    public required PaymentMethodType PaymentMethod { get; init; }

    public required string ConfirmationNumber { get; init; }

    public DateTimeOffset? ExpiresOn { get; init; }
}