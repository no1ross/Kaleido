//using Kaleido.Queryable.Attributes;
//using Kaleido.Queryable.Query;
//using System.ComponentModel;
//using System.ComponentModel.DataAnnotations;

//namespace Kaleido.Queryable.Shared;

//[NamedQuery(
//    Name ="active-records",
//    Version = "1.0",
//    DisplayName = "Active Records",
//    Description ="Returns only active records.")]
//public sealed class ActiveRecordsQuery :
//    INamedQueryContext<SampleKaleidoRecord>
//{
//    public IQueryable<SampleKaleidoRecord> Apply(IQueryable<SampleKaleidoRecord> query, 
//        EmptyNamedQueryParameters parameters, 
//        QueryExecutionContext context)
//    {
//        return query.Where(x => x.IsActive);
//    }
//}

//[NamedQuery(
//    Name = "records-by-category",
//    Version = "1.0",
//    DisplayName = "Records by Category",
//    Description = "Returns records by category.")]
//public sealed class RecordsByCategoryQuery :
//    INamedQueryContext<SampleKaleidoRecord, RecordsByCategoryQueryParameters>
//{
//    public IQueryable<SampleKaleidoRecord> Apply(
//        IQueryable<SampleKaleidoRecord> query,
//        RecordsByCategoryQueryParameters parameters,
//        QueryExecutionContext context)
//    {
//        return query.Where(x => x.Category == parameters.Category);
//    }
//}

//public class RecordsByCategoryQueryParameters
//{
//    [Required]
//    [Description("The category to filter records by.")]
//    public required string Category { get; set; }
//}



//[NamedQuery(
//    Name = "high-amount-records",
//    Version = "1.0",
//    DisplayName = "High Amount Records",
//    Description = "Returns records with amounts above a threshold.")]
//public sealed class HighAmountRecordsQuery :
//    INamedQueryContext<SampleKaleidoRecord, HighAmountRecordsQueryParameters>
//{
//    public IQueryable<SampleKaleidoRecord> Apply(
//        IQueryable<SampleKaleidoRecord> query,
//        HighAmountRecordsQueryParameters parameters,
//        QueryExecutionContext context)
//    {
//        return query.Where(x => x.Amount >= parameters.Amount);
//    }
//}

//public class HighAmountRecordsQueryParameters
//{
//    [Required]
//    [Description("The minimum amount that a record must have.")]
//    public required decimal Amount { get; set; }
//}


//[NamedQuery(
//    Name = "effective-on",
//    Version = "1.0",
//    DisplayName = "Effective On",
//    Description = "Returns records effective on a specific date.")]
//public sealed class EffectiveOnQuery :
//    INamedQueryContext<SampleKaleidoRecord, EffectiveOnQueryParameters>
//{
//    public IQueryable<SampleKaleidoRecord> Apply(
//        IQueryable<SampleKaleidoRecord> query,
//        EffectiveOnQueryParameters parameters,
//        QueryExecutionContext context)
//    {
//        var effectiveDate = parameters.EffectiveDate;;

//        return query.Where(
//            x =>
//                x.EffectiveDate <= effectiveDate &&
//                (x.ExpirationDate == null ||
//                 x.ExpirationDate >= effectiveDate));
//    }
//}
//public class EffectiveOnQueryParameters
//{
//    [Required]
//    [Description("The date to filter records by.")]
//    public required DateOnly EffectiveDate { get; set; }
//}