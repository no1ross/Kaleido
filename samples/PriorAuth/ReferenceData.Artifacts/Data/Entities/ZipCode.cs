namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;

public sealed class ZipCode
{
    public string PostalCode { get; set; } = string.Empty;

    public string StateCode { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public State State { get; set; } = null!;
}
