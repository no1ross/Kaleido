using Kaleido.Samples.PriorAuth.Member;

namespace Kaleido.Samples.PriorAuth.Member.Process.Responses;

public sealed record MemberSnapshotView
{
    public Guid MemberId { get; init; }

    public Guid MemberEnrollmentId { get; init; }

    public string MemberNumber { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateOnly DateOfBirth { get; init; }

    public string IssuanceState { get; init; } = string.Empty;

    public string AddressLine1 { get; init; } = string.Empty;

    public string? AddressLine2 { get; init; }

    public string City { get; init; } = string.Empty;

    public string AddressState { get; init; } = string.Empty;

    public string PostalCode { get; init; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; init; }

    public string PlanId { get; init; } = string.Empty;

    public string PlanName { get; init; } = string.Empty;

    public DateOnly EffectiveDate { get; init; }

    public DateOnly? TerminationDate { get; init; }

    public DateTimeOffset CapturedUtc { get; init; }
}
