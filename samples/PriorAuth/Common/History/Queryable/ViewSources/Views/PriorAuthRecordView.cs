namespace Kaleido.Samples.PriorAuth.History.Queryable.ViewSources.Views;

public sealed record PriorAuthRecordView
{
    public Guid PriorAuthRecordId { get; init; }

    public Guid ProcessId { get; init; }

    public string ProcessorName { get; init; } = string.Empty;

    public PriorAuthorizationStatus Status { get; init; }

    public string MemberNumber { get; init; } = string.Empty;

    public string MemberDisplayName { get; init; } = string.Empty;

    public DateOnly DateOfService { get; init; }

    public string PrimaryProcedureCode { get; init; } = string.Empty;

    public string PrimaryProcedureDescription { get; init; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; init; }

    public DateTimeOffset LastUpdatedUtc { get; init; }
}
