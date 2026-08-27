using Kaleido.Samples.PriorAuth.Configuration.Artifacts.Data.Entities;

namespace Kaleido.Samples.PriorAuth.Seeder.Configuration;

internal sealed class ConfigurationSeedAssets
{
    public required List<ProcedureModalityRule> ProcedureModalityRules { get; init; }

    public required List<MriProcedureCodeRule> MriProcedureCodeRules { get; init; }
}
