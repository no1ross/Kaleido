namespace Kaleido.Process.Registry;

internal sealed record ProcessStepDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    private readonly List<ProcessStepDefinition> _dependencies = [];
    private readonly List<ProcessStepDefinition> _availableAfter = [];
    private readonly List<ProcessStepDefinition> _availableUntil = [];

    public IReadOnlyCollection<ProcessStepDefinition> Dependencies => _dependencies;
    public IReadOnlyCollection<ProcessStepDefinition> AvailableAfter => _availableAfter;
    public IReadOnlyCollection<ProcessStepDefinition> AvailableUntil => _availableUntil;

    public void AddDependency(ProcessStepDefinition definition) => _dependencies.Add(definition);
    public void AddAvailableAfter(ProcessStepDefinition definition) => _availableAfter.Add(definition);
    public void AddAvailableUntil(ProcessStepDefinition definition) => _availableUntil.Add(definition);
}

internal sealed record ProcessStepTypeDefinition
{
    public required Type StepType { get; init; }

    public Type? StepResultType { get; init; }

    public required Type HandlerType { get; init; }

    public required ProcessStepMetadata Metadata { get; init; }

    private readonly List<Type> _dependencies = [];
    private readonly List<Type> _availableAfter = [];
    private readonly List<Type> _availableUntil = [];

    public IReadOnlyCollection<Type> Dependencies => _dependencies;
    public IReadOnlyCollection<Type> AvailableAfter => _availableAfter;
    public IReadOnlyCollection<Type> AvailableUntil => _availableUntil;

    public void AddDependency(Type type) => _dependencies.Add(type);
    public void AddAvailableAfter(Type type) => _availableAfter.Add(type);
    public void AddAvailableUntil(Type type) => _availableUntil.Add(type);
}