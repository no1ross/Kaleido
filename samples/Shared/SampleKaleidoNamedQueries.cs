using Kaleido.Queryable;

namespace Kaleido.Samples.Shared
{
    public sealed class ActiveRecordsQuery : IQueryableRecordNamedQuery<SampleKaleidoRecord>
    {
        public NamedQueryDescriptor Descriptor => 
            new NamedQueryDescriptor
            (
                Name: "active-records",
                Description: "Returns only active records."
            );

        public IQueryable<SampleKaleidoRecord> Apply(IQueryable<SampleKaleidoRecord> query, NamedQueryDescriptor NamedQuery)
        {
            return query.Where(x => x.IsActive);
        }
    }

    public sealed class RecordsByCategoryQuery : IQueryableRecordNamedQuery<SampleKaleidoRecord>
    {
        public NamedQueryDescriptor Descriptor => 
            new NamedQueryDescriptor
            (
                Name: "records-by-category",
                Description: "Returns records by category.",
                Parameters: new List<QueryParameterDescriptor>
                {
                    new QueryParameterDescriptor
                    (
                        Name: nameof(SampleKaleidoRecord.Category),
                        Description: "The category to filter records by.",
                        Required: true,
                        Type: typeof(SampleKaleidoRecord.Category)
                    )
                }
            );

        public IQueryable<SampleKaleidoRecord> Apply(IQueryable<SampleKaleidoRecord> query, NamedQueryDescriptor NamedQuery)
        {
            if (parameters is null || !parameters.TryGetValue("category", out var category) || category is null)
            {
                throw new InvalidOperationException("Named query 'records-by-category' requires parameter 'category'.");
            }

            var text = category.ToString();
            return query.Where(x => x.Category == text);
        }
    }

    public sealed class HighAmountRecordsQuery : IQueryableRecordNamedQuery<SampleKaleidoRecord>
    {
        public string Name => "high-amount-records";

        public IQueryable<SampleKaleidoRecord> Apply(IQueryable<SampleKaleidoRecord> query, IReadOnlyDictionary<string, object?>? parameters)
        {
            var minimumAmount = parameters is not null && parameters.TryGetValue("minimumAmount", out var value) && value is not null
                ? Convert.ToDecimal(value)
                : 100m;

            return query.Where(x => x.Amount >= minimumAmount);
        }
    }

    public sealed class EffectiveOnQuery : IQueryableRecordNamedQuery<SampleKaleidoRecord>
    {
        public string Name => "effective-on";

        public IQueryable<SampleKaleidoRecord> Apply(IQueryable<SampleKaleidoRecord> query, IReadOnlyDictionary<string, object?>? parameters)
        {
            if (parameters is null || !parameters.TryGetValue("effectiveDate", out var value) || value is null)
            {
                throw new InvalidOperationException("Named query 'effective-on' requires parameter 'effectiveDate'.");
            }

            var effectiveDate = DateOnly.Parse(value.ToString()!);
            return query.Where(x => x.EffectiveDate <= effectiveDate && (x.ExpirationDate == null || x.ExpirationDate >= effectiveDate));
        }
    }
}
