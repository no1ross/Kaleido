namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;

public sealed class PlanNetwork
{
    public string PlanId { get; set; } = string.Empty;

    public Guid NetworkId { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public bool IsPrimary { get; set; }

    public Plan Plan { get; set; } = null!;

    public Network Network { get; set; } = null!;
}
