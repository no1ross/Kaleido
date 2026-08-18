using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data.Entities;

namespace Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;

public sealed record ReferenceDataSeedModel(
    IReadOnlyList<State> States,
    IReadOnlyList<ZipCode> ZipCodes,
    IReadOnlyList<Plan> Plans);
