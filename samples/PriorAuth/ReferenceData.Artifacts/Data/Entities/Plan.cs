using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;

public sealed class Plan
{
    public string PlanId { get; set; } = string.Empty;

    public string PlanName { get; set; } = string.Empty;

    public LineOfBusiness LineOfBusiness { get; set; }

    public string StateCode { get; set; } = string.Empty;

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public bool IsActive { get; set; }

    public State State { get; set; } = null!;

    public ICollection<PlanNetwork> PlanNetworks { get; set; } = new List<PlanNetwork>();
}
