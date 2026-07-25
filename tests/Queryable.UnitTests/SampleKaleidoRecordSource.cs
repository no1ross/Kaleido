using Kaleido.Queryable.Records;
using Kaleido.Samples.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kaleido.Queryable.UnitTests
{
    public sealed class SampleKaleidoRecordSource : IQueryableRecordSource<SampleKaleidoRecord>
    {
        private readonly IReadOnlyCollection<SampleKaleidoRecord> _records;

        public SampleKaleidoRecordSource()
        {
            _records = FunctionalCsvLoader.Load(PathResolver.ResolveDataFile("functional-records.csv"));
        }

        public IQueryable<SampleKaleidoRecord> CreateQuery(RecordExecutionContext context)
        {
            return _records.AsQueryable();
        }
    }
}
