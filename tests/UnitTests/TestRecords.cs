//using Kaleido;
//using Kaleido.Attributes;
//namespace Queryable.Tests;

//[KaleidoRecord("Client", "1", "Demo")]
//[AllowedQuery("active", "Active")]
//[Pageable(2, 10)]
//public sealed class ClientRecord
//{
//    [Filterable(FilterOperator.Eq, FilterOperator.Contains, FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.In)]
//    [Searchable(1, MatchMode.StartsWith, MatchMode.Contains, MatchMode.Exact)]
//    [Sortable]
//    public string ClientName { get; init; } = string.Empty;

//    [Filterable(FilterOperator.Eq)]
//    [Sortable]
//    public string GroupName { get; init; } = string.Empty;

//    [Filterable(FilterOperator.Eq)]
//    public bool IsActive { get; init; }
//}

//public static class ClientRecords
//{
//    public static IReadOnlyList<ClientRecord> All => new[]
//    {
//        new ClientRecord { ClientName = "Blue Cross", GroupName = "Commercial", IsActive = true },
//        new ClientRecord { ClientName = "Blue Shield", GroupName = "Medicaid", IsActive = true },
//        new ClientRecord { ClientName = "Aetna", GroupName = "Commercial", IsActive = true },
//        new ClientRecord { ClientName = "Blue Legacy", GroupName = "Commercial", IsActive = false }
//    };
//}
