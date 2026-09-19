using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable.Query;

public interface IQueryableRegistry
{
    IReadOnlyCollection<QueryableContextRegistryItem> Registrations { get; }

    QueryableContextRegistryItem? Find(string name);

    QueryableContextRegistryItem GetRegistration(string name);
}
