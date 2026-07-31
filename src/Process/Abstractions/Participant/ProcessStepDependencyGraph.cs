namespace Kaleido.Process.Participant;

public sealed record ProcessStepDependencyGraph(
    IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> Dependencies,
    IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> Dependents);
