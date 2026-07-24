using Kaleido.Queryable.Metadata;

namespace Kaleido.Queryable;

public interface IQueryableRecordNamedQuery<TRecord> where TRecord : class
{
    NamedQueryMetadata Descriptor { get; }
    IQueryable<TRecord> Apply(IQueryable<TRecord> query, KaleidoNamedQuery NamedQuery);
}