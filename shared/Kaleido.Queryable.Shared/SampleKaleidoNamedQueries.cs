using Kaleido.Queryable.Attributes;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;

namespace Kaleido.Queryable.Shared;

[NamedQuery(
    Name ="active-records",
    DisplayName = "Active Records",
    Description ="Returns only active records.")]
public sealed class ActiveRecordsQuery :
    IRecordNamedQuery<SampleKaleidoRecord>
{
    public IQueryable<SampleKaleidoRecord> Apply(
        IQueryable<SampleKaleidoRecord> query,
        NamedQuery namedQuery)
    {
        return query.Where(x => x.IsActive);
    }
}

[NamedQuery(
    Name = "records-by-category",
    DisplayName = "Records by Category",
    Description = "Returns records by category.")]
[NamedQueryParameter(
    nameof(SampleKaleidoRecord.Category),
    typeof(string),
    Required = true,
    Description = "The category to filter records by.")]
public sealed class RecordsByCategoryQuery :
    IRecordNamedQuery<SampleKaleidoRecord>
{
    public IQueryable<SampleKaleidoRecord> Apply(
        IQueryable<SampleKaleidoRecord> query,
        NamedQuery namedQuery)
    {
        if (namedQuery.Parameters is null ||
            !namedQuery.Parameters.TryGetValue(
                nameof(SampleKaleidoRecord.Category),
                out var category) ||
            category is null)
        {
            throw new InvalidOperationException(
                $"Named query 'records-by-category' requires parameter '{nameof(SampleKaleidoRecord.Category)}'.");
        }

        var text = category.ToString();

        return query.Where(x => x.Category == text);
    }
}

[NamedQuery(
    Name = "high-amount-records",
    DisplayName = "High Amount Records",
    Description = "Returns records with amounts above a threshold.")]
[NamedQueryParameter(
    nameof(SampleKaleidoRecord.Amount),
    typeof(decimal),
    DefaultValue = 100d,
    Description = "Minimum amount that a record must have.")]
public sealed class HighAmountRecordsQuery :
    IRecordNamedQuery<SampleKaleidoRecord>
{
    public IQueryable<SampleKaleidoRecord> Apply(
        IQueryable<SampleKaleidoRecord> query,
        NamedQuery namedQuery)
    {
        var minimumAmount =
            namedQuery.Parameters is not null &&
            namedQuery.Parameters.TryGetValue(
                nameof(SampleKaleidoRecord.Amount),
                out var value) &&
            value is not null
                ? Convert.ToDecimal(value)
                : 25m;

        return query.Where(x => x.Amount >= minimumAmount);
    }
}

[NamedQuery(
    Name = "effective-on",
    DisplayName = "Effective On",
    Description = "Returns records effective on a specific date.")]
[NamedQueryParameter(
    nameof(SampleKaleidoRecord.EffectiveDate),
    typeof(DateOnly),
    Required = true,
    Description = "The date to filter records by.")]
public sealed class EffectiveOnQuery :
    IRecordNamedQuery<SampleKaleidoRecord>
{
    public IQueryable<SampleKaleidoRecord> Apply(
        IQueryable<SampleKaleidoRecord> query,
        NamedQuery namedQuery)
    {
        if (namedQuery.Parameters is null ||
            !namedQuery.Parameters.TryGetValue(
                nameof(SampleKaleidoRecord.EffectiveDate),
                out var value) ||
            value is null)
        {
            throw new InvalidOperationException(
                $"Named query 'effective-on' requires parameter '{nameof(SampleKaleidoRecord.EffectiveDate)}'.");
        }

        var effectiveDate =
            value is DateOnly date
                ? date
                : DateOnly.Parse(value.ToString()!);

        return query.Where(
            x =>
                x.EffectiveDate <= effectiveDate &&
                (x.ExpirationDate == null ||
                 x.ExpirationDate >= effectiveDate));
    }
}