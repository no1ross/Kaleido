namespace Kaleido.Samples.PriorAuth.Radiology.Data.Entities;

public sealed class PriorAuthorizationRequestedService
{
    public Guid PriorAuthorizationRequestedServiceId { get; set; }

    public Guid PriorAuthorizationId { get; set; }

    public Guid? UserEnteredProcedureCodeId { get; set; }

    public string UserEnteredCodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem UserEnteredCodeSystem { get; set; }

    public Guid? ResolvedProcedureCodeId { get; set; }

    public string ResolvedCodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem ResolvedCodeSystem { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// What the caller stated about this service up front, by binding key (e.g. <c>Laterality</c> →
    /// <c>Left</c>), as JSON. Configured questions with the same binding key propose these values for
    /// this service; the caller confirms them.
    /// </summary>
    public string? StatedDetailsJson { get; set; }

    public PriorAuthorization PriorAuthorization { get; set; } = null!;
}
