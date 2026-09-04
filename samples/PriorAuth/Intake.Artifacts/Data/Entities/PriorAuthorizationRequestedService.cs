using Kaleido.Samples.PriorAuth.CodeSet;

namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

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

    public PriorAuthorization PriorAuthorization { get; set; } = null!;
}
