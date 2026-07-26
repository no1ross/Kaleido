using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;

namespace Kaleido.Queryable.Records;

public interface IRecordNamedQuery<TRecord> where TRecord : class
{
    IQueryable<TRecord> Apply(IQueryable<TRecord> query, NamedQuery NamedQuery);
}