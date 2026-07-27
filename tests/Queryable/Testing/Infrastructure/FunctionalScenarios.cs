using Kaleido.Queryable;
using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Shared;
using System.Globalization;

namespace Kaleido.Queryable.FunctionalTests.Infrastructure;

public static class FunctionalScenarios
{
    // Keep this in sync with [Pageable(25, 500)] on SampleKaleidoRecord.
    private const int DefaultPageSize = 25;

    public static IEnumerable<object[]> All()
    {
        foreach (var scenario in Create())
        {
            yield return new object[] { scenario };
        }
    }

    public static IReadOnlyList<FunctionalScenario> Create()
    {
        return new List<FunctionalScenario>
        {
            QueryAll(),
            FilterByCategory(),
            SearchSpecificField(),
            SortByCodeDescending(),
            PageSecondPage(),
            NamedQueryActiveRecords(),
            NamedQueryRecordsByCategory(),
            NamedQueryHighAmountRecords(),
            NamedQueryEffectiveOn(),
            CombinedFilterSearchSortPage()
        };
    }

    private static FunctionalScenario QueryAll()
    {
        return Scenario(
            "query-all-sorted-by-id",
            data => Request(
                sort: SortById()),
            data => data
                .OrderBy(x => x.Id)
                .ToList(),
            data => DefaultPage(
                data.OrderBy(x => x.Id)));
    }

    private static FunctionalScenario FilterByCategory()
    {
        return Scenario(
            "filter-category-eq",
            data => Request(
                filter: FilterCondition(
                    "Category",
                    FilterOperator.Equals,
                    FirstCategory(data)),
                sort: SortById()),
            data => data
                .Where(x => x.Category == FirstCategory(data))
                .OrderBy(x => x.Id)
                .ToList(),
            data => DefaultPage(
                data
                    .Where(x => x.Category == FirstCategory(data))
                    .OrderBy(x => x.Id)));
    }

    private static FunctionalScenario SearchSpecificField()
    {
        return Scenario(
            "search-region-contains",
            data => Request(
                search: SearchCondition(
                    FirstRegion(data),
                    MatchMode.Contains,
                    "Region"),
                sort: SortById()),
            data => data
                .Where(x => Contains(
                    x.Region,
                    FirstRegion(data)))
                .OrderBy(x => x.Id)
                .ToList(),
            data => DefaultPage(
                data
                    .Where(x => Contains(
                        x.Region,
                        FirstRegion(data)))
                    .OrderBy(x => x.Id)));
    }

    private static FunctionalScenario SortByCodeDescending()
    {
        return Scenario(
            "sort-code-descending",
            data => Request(
                sort: new List<QuerySort>
                {
                    new(
                        "Code",
                        SortDirection.Descending)
                }),
            data => data
                .OrderByDescending(x => x.Code)
                .ToList(),
            data => DefaultPage(
                data.OrderByDescending(x => x.Code)));
    }

    private static FunctionalScenario PageSecondPage()
    {
        const int size = 5;
        const int offset = 5;

        return Scenario(
            "page-second-page-sorted-by-id",
            data => Request(
                sort: SortById(),
                page: new QueryPage(
                    Size: size,
                    Offset: offset)),
            data => data
                .OrderBy(x => x.Id)
                .ToList(),
            data => data
                .OrderBy(x => x.Id)
                .Skip(offset)
                .Take(size)
                .ToList());
    }

    private static FunctionalScenario NamedQueryActiveRecords()
    {
        return Scenario(
            "named-active-records",
            data => new QueryRequest(
                NamedQuery: new NamedQuery(
                    "active-records"),
                Query: new QueryBody(
                    Search: null,
                    Filter: null,
                    Sort: SortById(),
                    Page: null)),
            data => data
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id)
                .ToList(),
            data => DefaultPage(
                data
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.Id)));
    }

    private static FunctionalScenario NamedQueryRecordsByCategory()
    {
        return Scenario(
            "named-records-by-category",
            data => new QueryRequest(
                NamedQuery: new NamedQuery(
                    "records-by-category",
                    new Dictionary<string, object?>
                    {
                        ["Category"] = FirstCategory(data)
                    }),
                Query: new QueryBody(
                    Search: null,
                    Filter: null,
                    Sort: SortById(),
                    Page: null)),
            data => data
                .Where(x => x.Category == FirstCategory(data))
                .OrderBy(x => x.Id)
                .ToList(),
            data => DefaultPage(
                data
                    .Where(x => x.Category == FirstCategory(data))
                    .OrderBy(x => x.Id)));
    }

    private static FunctionalScenario NamedQueryHighAmountRecords()
    {
        return Scenario(
            "named-high-amount-records",
            data => new QueryRequest(
                NamedQuery: new NamedQuery(
                    "high-amount-records",
                    new Dictionary<string, object?>
                    {
                        ["Amount"] = MedianAmount(data)
                    }),
                Query: new QueryBody(
                    Search: null,
                    Filter: null,
                    Sort: SortById(),
                    Page: null)),
            data => data
                .Where(x => x.Amount >= MedianAmount(data))
                .OrderBy(x => x.Id)
                .ToList(),
            data => DefaultPage(
                data
                    .Where(x => x.Amount >= MedianAmount(data))
                    .OrderBy(x => x.Id)));
    }

    private static FunctionalScenario NamedQueryEffectiveOn()
    {
        return Scenario(
            "named-effective-on",
            data => new QueryRequest(
                NamedQuery: new NamedQuery(
                    "effective-on",
                    new Dictionary<string, object?>
                    {
                        ["EffectiveDate"] = MedianEffectiveDate(data)
                    }),
                Query: new QueryBody(
                    Search: null,
                    Filter: null,
                    Sort: SortById(),
                    Page: null)),
            data => EffectiveOn(data)
                .OrderBy(x => x.Id)
                .ToList(),
            data => DefaultPage(
                EffectiveOn(data)
                    .OrderBy(x => x.Id)));
    }

    private static FunctionalScenario CombinedFilterSearchSortPage()
    {
        const int size = 5;
        const int offset = 0;

        return Scenario(
            "combined-filter-search-sort-page",
            data =>
            {
                var seed =
                    FirstActiveRecord(data);

                return Request(
                    filter: FilterGroup(
                        LogicalOperator.And,
                        FilterCondition(
                            "Category",
                            FilterOperator.Equals,
                            seed.Category),
                        FilterCondition(
                            "IsActive",
                            FilterOperator.IsTrue)),
                    search: SearchCondition(
                        seed.Region,
                        MatchMode.Contains,
                        "Region"),
                    sort: new List<QuerySort>
                    {
                        new(
                            "Name",
                            SortDirection.Ascending,
                            1),
                        new(
                            "Id",
                            SortDirection.Ascending,
                            2)
                    },
                    page: new QueryPage(
                        Size: size,
                        Offset: offset));
            },
            data => Combined(data)
                .ToList(),
            data => Combined(data)
                .Skip(offset)
                .Take(size)
                .ToList());
    }

    private static IReadOnlyList<SampleKaleidoRecord> DefaultPage(
        IEnumerable<SampleKaleidoRecord> records)
    {
        return records
            .Take(DefaultPageSize)
            .ToList();
    }

    private static IEnumerable<SampleKaleidoRecord> EffectiveOn(
        IReadOnlyList<SampleKaleidoRecord> data)
    {
        var effectiveDate =
            MedianEffectiveDate(data);

        return data
            .Where(x =>
                x.EffectiveDate <= effectiveDate)
            .Where(x =>
                x.ExpirationDate is null ||
                x.ExpirationDate >= effectiveDate);
    }

    private static IEnumerable<SampleKaleidoRecord> Combined(
        IReadOnlyList<SampleKaleidoRecord> data)
    {
        var seed =
            FirstActiveRecord(data);

        return data
            .Where(x =>
                x.Category == seed.Category)
            .Where(x =>
                x.IsActive)
            .Where(x =>
                Contains(
                    x.Region,
                    seed.Region))
            .OrderBy(x =>
                x.Name)
            .ThenBy(x =>
                x.Id);
    }

    private static FunctionalScenario Scenario(
        string name,
        Func<IReadOnlyList<SampleKaleidoRecord>, QueryRequest> createRequest,
        Func<IReadOnlyList<SampleKaleidoRecord>, IReadOnlyList<SampleKaleidoRecord>> expectedUnpaged,
        Func<IReadOnlyList<SampleKaleidoRecord>, IReadOnlyList<SampleKaleidoRecord>> expectedPaged)
    {
        return new FunctionalScenario(
            name,
            createRequest,
            expectedUnpaged,
            expectedPaged);
    }

    private static QueryRequest Request(
        QuerySearchNode? search = null,
        QueryFilterNode? filter = null,
        IReadOnlyList<QuerySort>? sort = null,
        QueryPage? page = null)
    {
        return new QueryRequest(
            Query: new QueryBody(
                Search: search,
                Filter: filter,
                Sort: sort,
                Page: page));
    }

    private static QueryFilterNode FilterCondition(
        string field,
        FilterOperator @operator,
        params object?[] values)
    {
        return QueryFilterNode.CreateCondition(
            field,
            @operator,
            values);
    }

    private static QueryFilterNode FilterGroup(
        LogicalOperator @operator,
        params QueryFilterNode[] filters)
    {
        return QueryFilterNode.CreateGroup(
            @operator,
            filters);
    }

    private static QuerySearchNode SearchCondition(
        string searchText,
        MatchMode matchMode,
        string? field = null)
    {
        return QuerySearchNode.CreateCondition(
            searchText,
            matchMode,
            field);
    }

    private static IReadOnlyList<QuerySort> SortById()
    {
        return new List<QuerySort>
        {
            new(
                "Id",
                SortDirection.Ascending)
        };
    }

    private static string FirstCategory(
        IReadOnlyList<SampleKaleidoRecord> data)
    {
        return data
            .Select(x => x.Category)
            .First(x =>
                !string.IsNullOrWhiteSpace(x));
    }

    private static string FirstRegion(
        IReadOnlyList<SampleKaleidoRecord> data)
    {
        return data
            .Select(x => x.Region)
            .First(x =>
                !string.IsNullOrWhiteSpace(x));
    }

    private static SampleKaleidoRecord FirstActiveRecord(
        IReadOnlyList<SampleKaleidoRecord> data)
    {
        return data.First(x =>
            x.IsActive &&
            !string.IsNullOrWhiteSpace(x.Region));
    }

    private static decimal MedianAmount(
        IReadOnlyList<SampleKaleidoRecord> data)
    {
        var values =
            data
                .Select(x => x.Amount)
                .OrderBy(x => x)
                .ToArray();

        return values[values.Length / 2];
    }

    private static DateOnly MedianEffectiveDate(
        IReadOnlyList<SampleKaleidoRecord> data)
    {
        var values =
            data
                .Select(x => x.EffectiveDate)
                .OrderBy(x => x)
                .ToArray();

        return values[values.Length / 2];
    }

    private static bool Contains(
        string value,
        string searchText)
    {
        return value.Contains(
            searchText,
            StringComparison.OrdinalIgnoreCase);
    }
}