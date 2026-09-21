namespace Kaleido.Samples.PriorAuth.ReferenceData.Data.Entities;

public sealed class Network
{
    public Guid NetworkId { get; set; }

    public string NetworkCode { get; set; } = string.Empty;

    public string NetworkName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateOnly? TerminationDate { get; set; }

    public bool IsActive { get; set; }

    public ICollection<PlanNetwork> PlanNetworks { get; set; } = new List<PlanNetwork>();
}
