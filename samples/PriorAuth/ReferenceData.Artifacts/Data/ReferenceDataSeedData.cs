using Kaleido.Samples.PriorAuth.ReferenceData.Data.Entities;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Data;

public sealed record ReferenceDataSeedModel(
    IReadOnlyList<State> States,
    IReadOnlyList<ZipCode> ZipCodes,
    IReadOnlyList<Plan> Plans,
    IReadOnlyList<Network> Networks,
    IReadOnlyList<PlanNetwork> PlanNetworks);
