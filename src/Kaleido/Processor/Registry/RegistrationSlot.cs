namespace Kaleido.Processor.Registry;

internal sealed class RegistrationSlot
{
    private readonly List<ProcessStepRegistration> _dependencies = [];
    private readonly List<ProcessStepRegistration> _availableAfter = [];
    private readonly List<ProcessStepRegistration> _availableUntil = [];

    public RegistrationSlot(
        ProcessStepDefinition definition,
        RepeatableOptions repeatable,
        Func<Task, IProcessStepHandlerResult>? getResultFromTask,
        Func<object, object, ProcessStepContext, CancellationToken, Task>? invokeHandlerAsync)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Definition = definition;

        Registration =
            new ProcessStepRegistration(
                definition.StepType,
                definition.StepResultType,
                definition.HandlerType,
                _dependencies.AsReadOnly(),
                _availableAfter.AsReadOnly(),
                _availableUntil.AsReadOnly(),
                repeatable,
                definition.Metadata,
                getResultFromTask,
                invokeHandlerAsync);
    }

    public ProcessStepDefinition Definition
    {
        get;
    }

    public ProcessStepRegistration Registration
    {
        get;
    }

    public IReadOnlyCollection<ProcessStepRegistration> Dependencies => _dependencies;
    public IReadOnlyCollection<ProcessStepRegistration> AvailableAfter => _availableAfter;
    public IReadOnlyCollection<ProcessStepRegistration> AvailableUntil => _availableUntil;

    public void AddDependencies(IEnumerable<ProcessStepRegistration> items) => _dependencies.AddRange(items);
    public void AddAvailableAfter(IEnumerable<ProcessStepRegistration> items) => _availableAfter.AddRange(items);
    public void AddAvailableUntil(IEnumerable<ProcessStepRegistration> items) => _availableUntil.AddRange(items);
}
