namespace Kaleido.Samples.PriorAuth.Configuration.Data.Entities;

public sealed class ProductCodeMapping
{
    public Guid ProductCodeMappingId { get; set; }

    public string CodeValue { get; set; } = string.Empty;

    public ProcedureCodeSystem CodeSystem { get; set; }

    public string ProcessorName { get; set; } = string.Empty;
}
