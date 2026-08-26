using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Data.Entities;

public sealed class PriorAuthorizationRequestedService
{
    public Guid PriorAuthorizationRequestedServiceId { get; set; }

    public Guid PriorAuthorizationId { get; set; }

    public Guid? ProcedureCodeId { get; set; }

    public string CodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem CodeSystem { get; set; }

    public string Description { get; set; } = string.Empty;

    public PriorAuthorization PriorAuthorization { get; set; } = null!;
}
