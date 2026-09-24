namespace Kaleido.Process.Registry;

[ExcludeFromCodeCoverage]
public sealed record ProcessStepDependencyGraph(
    IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> Dependencies,
    IReadOnlyDictionary<Type, IReadOnlyCollection<Type>> Dependents);
