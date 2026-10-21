namespace Kaleido.Processor.Registry;

internal sealed record ProcessStepDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    // Relationship attributes are collected as Types first — the target
    // definitions don't exist until every step has been materialized.
    private readonly List<Type> _dependencyTypes = [];
    private readonly List<Type> _availableAfterTypes = [];
    private readonly List<Type> _availableUntilTypes = [];

    public IReadOnlyCollection<Type> DependencyTypes => _dependencyTypes;
    public IReadOnlyCollection<Type> AvailableAfterTypes => _availableAfterTypes;
    public IReadOnlyCollection<Type> AvailableUntilTypes => _availableUntilTypes;

    private readonly List<ProcessStepDefinition> _dependencies = [];
    private readonly List<ProcessStepDefinition> _availableAfter = [];
    private readonly List<ProcessStepDefinition> _availableUntil = [];

    public IReadOnlyCollection<ProcessStepDefinition> Dependencies => _dependencies;
    public IReadOnlyCollection<ProcessStepDefinition> AvailableAfter => _availableAfter;
    public IReadOnlyCollection<ProcessStepDefinition> AvailableUntil => _availableUntil;

    public void AddDependencyType(Type type) => _dependencyTypes.Add(type);
    public void AddAvailableAfterType(Type type) => _availableAfterTypes.Add(type);
    public void AddAvailableUntilType(Type type) => _availableUntilTypes.Add(type);

    public void AddDependency(ProcessStepDefinition definition) => _dependencies.Add(definition);
    public void AddAvailableAfter(ProcessStepDefinition definition) => _availableAfter.Add(definition);
    public void AddAvailableUntil(ProcessStepDefinition definition) => _availableUntil.Add(definition);
}
