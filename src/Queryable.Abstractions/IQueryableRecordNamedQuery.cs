namespace Kaleido.Queryable
{
    public interface IQueryableRecordNamedQuery<TRecord>
        where TRecord : class
    {
        string Name { get; }
        IQueryable<TRecord> Apply(IQueryable<TRecord> query, IReadOnlyDictionary<string, object?>? parameters);
    }
}