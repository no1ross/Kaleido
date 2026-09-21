namespace Kaleido.Samples.PriorAuth.History.Data.Entities;

public sealed class PriorAuthRecord
{
    public Guid PriorAuthRecordId { get; set; }

    public Guid ProcessId { get; set; }

    public string ProcessorName { get; set; } = string.Empty;

    public PriorAuthorizationStatus Status { get; set; }

    public string MemberNumber { get; set; } = string.Empty;

    public string MemberDisplayName { get; set; } = string.Empty;

    public DateOnly DateOfService { get; set; }

    public string PrimaryProcedureCode { get; set; } = string.Empty;

    public string PrimaryProcedureDescription { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset LastUpdatedUtc { get; set; }
}
