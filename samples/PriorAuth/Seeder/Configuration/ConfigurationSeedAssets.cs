using Kaleido.Samples.PriorAuth.Configuration.Data.Entities;

namespace Kaleido.Samples.PriorAuth.Seeder.Configuration;

internal sealed class ConfigurationSeedAssets
{
    public required List<ProcedureModalityRule> ProcedureModalityRules { get; init; }

    public required List<MriProcedureCodeRule> MriProcedureCodeRules { get; init; }

    public required List<QuestionnaireDefinition> QuestionnaireDefinitions { get; init; }
}
