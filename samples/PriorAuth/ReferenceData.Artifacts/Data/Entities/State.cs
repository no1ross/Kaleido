namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;

public sealed class State
{
    public string StateCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public ICollection<ZipCode> ZipCodes { get; set; } = new List<ZipCode>();

    public ICollection<Plan> Plans { get; set; } = new List<Plan>();
}
