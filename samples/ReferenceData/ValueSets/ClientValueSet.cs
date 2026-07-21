//using Kaleido;
//using Kaleido.Attributes;
//using Kaleido.Queryable;
//using Microsoft.EntityFrameworkCore;
//using System.ComponentModel.DataAnnotations;

//namespace ReferenceData.ValueSets;

//[KaleidoRecord("clients", "1.0.0", "SQLite Test Data")]
//[AllowedQuery("clients-by-group", "Returns clients for a supplied group.", "groupName")]
//[AllowedQuery("clients-searchable", "Returns clients intended for search tests.")]
//[Pageable(50, 500)]
//public sealed class ClientRecord
//{
//    [Key]
//    [Filterable(FilterOperator.Eq, FilterOperator.In, FilterOperator.StartsWith)]
//    [Searchable(1, MatchMode.Exact, MatchMode.StartsWith, MatchMode.Contains)]
//    [Sortable()]
//    public string ClientId { get; init; } = string.Empty;

//    [Filterable(FilterOperator.Eq, FilterOperator.Contains, FilterOperator.StartsWith)]
//    [Searchable(2, MatchMode.Exact, MatchMode.StartsWith, MatchMode.Contains)]
//    [Sortable()]
//    public string ClientName { get; init; } = string.Empty;

//    [Filterable(FilterOperator.Eq, FilterOperator.In)]
//    [Searchable(4, MatchMode.Exact, MatchMode.Contains)]
//    [Sortable()]
//    public string GroupName { get; init; } = string.Empty;

//    [Filterable(FilterOperator.Eq, FilterOperator.Contains, FilterOperator.StartsWith)]
//    [Searchable(3, MatchMode.Exact, MatchMode.StartsWith, MatchMode.Contains)]
//    [Sortable()]
//    public string DisplayName { get; init; } = string.Empty;
//}

//public sealed class ClientValueSetSource : IQueryableRecordSource<ClientRecord>
//{
//    private readonly KaleidoTestDbContext _db;

//    public ClientValueSetSource(KaleidoTestDbContext db) => _db = db;

//    public IQueryable<ClientRecord> CreateQuery(RecordExecutionContext context)
//    {
//        return _db.Records.AsNoTracking();
//    }
//}

//public sealed class ClientsByGroupQuery : IQueryableRecordNamedQuery<ClientRecord>
//{
//    public string Name => "clients-by-group";

//    public IQueryable<ClientRecord> Apply(IQueryable<ClientRecord> query, IReadOnlyDictionary<string, object?>? parameters)
//    {
//        var groupName = parameters?.TryGetValue("groupName", out var value) == true
//            ? value?.ToString()
//            : null;

//        return string.IsNullOrWhiteSpace(groupName)
//            ? query
//            : query.Where(x => x.GroupName == groupName);
//    }
//}

//public sealed class ClientsSearchableQuery : IQueryableRecordNamedQuery<ClientRecord>
//{
//    public string Name => "clients-searchable";

//    public IQueryable<ClientRecord> Apply(IQueryable<ClientRecord> query, IReadOnlyDictionary<string, object?>? parameters)
//    {
//        try
//        {
//            return query.Where(x => x.ClientName != string.Empty && x.DisplayName != string.Empty);
//        }
//        catch (Exception)
//        {
//            throw;
//        }
//    }
//}
