namespace Kaleido.Process.Participant.Registry;

public sealed record ProcessStepDependencyGraph(
    IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> Dependencies,
    IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> Dependents);
