using System.ComponentModel;

namespace Kaleido.Samples.PriorAuth.History.Queryable.ViewSources.Views;

public sealed record PriorAuthRecordView
{
    [Description("The ID of the prior authorization process this record belongs to; there is one record per process.")]
    public Guid ProcessId { get; init; }

    [Description("The name of the processor that recorded the latest snapshot, such as intake or radiology.")]
    public string ProcessorName { get; init; } = string.Empty;

    [Description("The prior authorization ID assigned by the product processor (Radiology); null while the request is in Intake.")]
    public Guid? PriorAuthorizationId { get; init; }

    [Description("The current status of the request: Unknown, Draft (in progress) or Submitted.")]
    public PriorAuthorizationStatus Status { get; init; }

    [Description("The member number of the member the request is for; empty if no member has been captured yet.")]
    public string MemberNumber { get; init; } = string.Empty;

    [Description("The display name of the member the request is for; empty if no member has been captured yet.")]
    public string MemberDisplayName { get; init; } = string.Empty;

    [Description("The planned date of service; null if not yet known.")]
    public DateOnly? DateOfService { get; init; }

    [Description("The procedure code of the main service requested; empty if no service has been captured yet.")]
    public string PrimaryProcedureCode { get; init; } = string.Empty;

    [Description("The readable description of the main service requested; empty if no service has been captured yet.")]
    public string PrimaryProcedureDescription { get; init; } = string.Empty;

    [Description("When the record was first created, in UTC.")]
    public DateTimeOffset CreatedUtc { get; init; }

    [Description("When the latest snapshot was recorded, in UTC.")]
    public DateTimeOffset LastUpdatedUtc { get; init; }
}
