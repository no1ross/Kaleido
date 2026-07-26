using System;
using System.Collections.Generic;
using System.Text;

namespace Kaleido.Queryable.Records
{
    public interface IRecordSource<TRecord>
            where TRecord : class
    {
        IQueryable<TRecord> CreateQuery(RecordExecutionContext executionContext);
    }
}