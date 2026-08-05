using Kaleido.Process.Participant.Registry;

public interface IProcessStepRegistry
{
    IReadOnlyCollection<ProcessStepRegistration> Registrations { get; }

    ProcessStepRegistration? Find(string name);

    ProcessStepRegistration? Find(Type stepType);

    ProcessStepRegistration GetRegistration(string name);

    ProcessStepRegistration GetRegistration(Type stepType);
}
