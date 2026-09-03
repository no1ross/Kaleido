using Kaleido.Process.Registry;

public interface IProcessStepRegistry
{
    IReadOnlyCollection<ProcessStepRegistration> Registrations { get; }

    IReadOnlyCollection<ProcessStepRegistration> InitialRegistrations { get; }

    ProcessStepRegistration? Find(string name);

    ProcessStepRegistration? Find(Type stepType);

    ProcessStepRegistration GetRegistration(string name);

    ProcessStepRegistration GetRegistration(Type stepType);
}
