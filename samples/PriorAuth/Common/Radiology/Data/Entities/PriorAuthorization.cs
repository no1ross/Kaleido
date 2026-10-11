namespace Kaleido.Samples.PriorAuth.Radiology.Data.Entities;

public sealed class PriorAuthorization
{
    public Guid PriorAuthorizationId { get; set; }

    public Guid ProcessId { get; set; }

    public PriorAuthorizationStatus Status { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? SubmittedUtc { get; set; }

    /// <summary>The clinical question provider's session or assessment id, kept between rounds.</summary>
    public string? ClinicalSessionId { get; set; }

    /// <summary>The provider's id of the clinical round being asked (null when none is pending).</summary>
    public string? ClinicalRoundId { get; set; }

    /// <summary>How many clinical rounds were presented.</summary>
    public int ClinicalRoundCount { get; set; }

    /// <summary>The provider's determination when the clinical review finished (recorded, not decided here).</summary>
    public string? ClinicalDetermination { get; set; }

    public PriorAuthorizationMember? Member { get; set; }

    public PriorAuthorizationRequestingProvider? RequestingProvider { get; set; }

    public PriorAuthorizationServicingProvider? ServicingProvider { get; set; }

    public ICollection<PriorAuthorizationRequestedService> RequestedServices { get; set; } = new List<PriorAuthorizationRequestedService>();
}
