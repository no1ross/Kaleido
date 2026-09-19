using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

public interface IDelegatedQueryViewRegistry
{
    IReadOnlyCollection<DelegatedQueryViewRegistration> Registrations { get; }

    DelegatedQueryViewRegistration? Find(string name);

    DelegatedQueryViewRegistration? Find(Type recordType);

    DelegatedQueryViewRegistration GetRegistration(string name);

    DelegatedQueryViewRegistration GetRegistration(Type recordType);
}
