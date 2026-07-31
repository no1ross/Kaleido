using Kaleido.Process.Participant;

public interface IProcessStepRegistry
{
    IReadOnlyCollection<ProcessStepRegistration> Registrations { get; }

    ProcessStepDependencyGraph Graph { get; }

    ProcessStepRegistration? Find(string name);

    ProcessStepRegistration? Find(Type stepType);

    ProcessStepRegistration GetRegistration(string name);

    ProcessStepRegistration GetRegistration(Type stepType);

    bool HasDependencies(Type stepType);

    bool HasDependents(Type stepType);

    IReadOnlyCollection<ProcessStepRegistration> GetDependencies(Type stepType);

    IReadOnlyCollection<ProcessStepRegistration> GetDependents(Type stepType);

    IReadOnlyCollection<ProcessStepRegistration> GetDependencyChain(Type stepType);

    IReadOnlyCollection<ProcessStepRegistration> GetDependentChain(Type stepType);
}
