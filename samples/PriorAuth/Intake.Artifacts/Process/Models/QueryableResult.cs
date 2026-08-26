namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;

public sealed record QueryableResult<TRecord>
{
    public IReadOnlyCollection<TRecord> Records { get; init; } = [];
}
