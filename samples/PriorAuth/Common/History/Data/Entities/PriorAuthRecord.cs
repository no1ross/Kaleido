using Kaleido.Samples.PriorAuth.Audit;

namespace Kaleido.Samples.PriorAuth.History.Data.Entities;

/// <summary>
/// The latest information for one prior authorization request, keyed by its process: one record per
/// process, overwritten by every recording the processors make as the request moves on.
/// </summary>
public sealed class PriorAuthRecord : IAuditable
{
    public Guid ProcessId { get; set; }

    /// <summary>The processor that recorded last (the calling processor Kaleido stamps on the call).</summary>
    public string ProcessorName { get; set; } = string.Empty;

    /// <summary>The product processor's prior authorization (e.g. Radiology); null while in Intake.</summary>
    public Guid? PriorAuthorizationId { get; set; }

    public PriorAuthorizationStatus Status { get; set; }

    public string MemberNumber { get; set; } = string.Empty;

    public string MemberDisplayName { get; set; } = string.Empty;

    public DateOnly? DateOfService { get; set; }

    public string PrimaryProcedureCode { get; set; } = string.Empty;

    public string PrimaryProcedureDescription { get; set; } = string.Empty;

    /// <inheritdoc />
    public string CreatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? CreatedVia { get; set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedUtc { get; set; }

    /// <inheritdoc />
    public string UpdatedBy { get; set; } = string.Empty;

    /// <inheritdoc />
    public string? UpdatedVia { get; set; }

    /// <inheritdoc />
    public DateTimeOffset UpdatedUtc { get; set; }
}
