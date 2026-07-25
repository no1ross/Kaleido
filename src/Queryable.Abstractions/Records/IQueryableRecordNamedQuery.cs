using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Records;

public interface IQueryableRecordNamedQuery<TRecord> where TRecord : class
{
    IQueryable<TRecord> Apply(IQueryable<TRecord> query, KaleidoNamedQuery NamedQuery);
}