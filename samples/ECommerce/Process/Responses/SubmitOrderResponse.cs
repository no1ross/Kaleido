namespace Kaleido.Samples.ECommerce.Process.Responses;

public sealed record SubmitOrderResponse
{
    public required string SubmissionId { get; init; }

    public required bool Submitted { get; init; }

    public bool RequiresPaymentCorrection { get; init; }

    public IReadOnlyCollection<ProcessIssue> Issues
    {
        get;
        init;
    } = [];
}
