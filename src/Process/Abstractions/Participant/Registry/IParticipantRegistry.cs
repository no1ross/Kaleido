namespace Kaleido.Process.Participant.Registry;

public interface IParticipantRegistry
{
    IReadOnlyCollection<ParticipantRegistryItem> Registrations { get; }

    ParticipantRegistryItem? Find(string name);

    ParticipantRegistryItem GetRegistration(string name);
}
