using Kaleido.Samples.PriorAuth.CodeSet;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Models;

public sealed record ProcedureCodeRecord
{
    public Guid ProcedureCodeId { get; init; }

    public string CodeValue { get; init; } = string.Empty;

    public ProcedureCodeSystem CodeSystem { get; init; }

    public string ShortDescription { get; init; } = string.Empty;

    public string? LongDescription { get; init; }
}
