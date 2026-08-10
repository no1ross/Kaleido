namespace Kaleido.Queryable.Records;

public interface IRecordSource<TRecord>
        where TRecord : class
{
    IQueryable<TRecord> CreateQuery(RecordExecutionContext executionContext);
}

public interface IRecordView<TRecord, TView>
        where TRecord : class
        where TView : class
{
    IQueryable<TView> CreateView(IQueryable<TRecord> query, RecordExecutionContext executionContext);
}