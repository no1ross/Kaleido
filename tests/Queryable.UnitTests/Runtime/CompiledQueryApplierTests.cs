using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Runtime;
using System.ComponentModel;
using Xunit;

namespace Kaleido.Queryable.UnitTests.Runtime;

public sealed class CompiledQueryApplierTests
{
    private readonly CompiledQueryApplier<TestRecord> _applier = new();

    [Fact]
    public void ApplyFilter_ShouldReturnOriginalQuery_WhenFilterIsNull()
    {
        var query =
            CreateRecords()
                .AsQueryable();

        var result =
            _applier.ApplyFilter(
                query,
                null);

        Assert.Same(
            query,
            result);
    }

    [Fact]
    public void ApplySearch_ShouldReturnOriginalQuery_WhenSearchIsNull()
    {
        var query =
            CreateRecords()
                .AsQueryable();

        var result =
            _applier.ApplySearch(
                query,
                null);

        Assert.Same(
            query,
            result);
    }

    [Fact]
    public void ApplyFilter_ShouldApplyEquals()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        FilterOperator.Eq,
                        "Alpha"))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            "Alpha",
            item.Name);
    }

    [Fact]
    public void ApplyFilter_ShouldApplyNotEquals()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        FilterOperator.Ne,
                        "Alpha"))
                .ToArray();

        Assert.DoesNotContain(
            result,
            x => x.Name == "Alpha");
    }

    [Fact]
    public void ApplyFilter_ShouldApplyGreaterThan()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Amount), typeof(decimal)),
                        FilterOperator.Gt,
                        100m))
                .ToArray();

        Assert.All(
            result,
            x => Assert.True(x.Amount > 100m));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyGreaterThanOrEqual()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Amount), typeof(decimal)),
                        FilterOperator.Gte,
                        100m))
                .ToArray();

        Assert.All(
            result,
            x => Assert.True(x.Amount >= 100m));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyLessThan()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Amount), typeof(decimal)),
                        FilterOperator.Lt,
                        100m))
                .ToArray();

        Assert.All(
            result,
            x => Assert.True(x.Amount < 100m));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyLessThanOrEqual()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Amount), typeof(decimal)),
                        FilterOperator.Lte,
                        100m))
                .ToArray();

        Assert.All(
            result,
            x => Assert.True(x.Amount <= 100m));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyContains()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        FilterOperator.Contains,
                        "ph"))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            "Alpha",
            item.Name);
    }

    [Fact]
    public void ApplyFilter_ShouldApplyNotContains()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        FilterOperator.NotContains,
                        "ph"))
                .ToArray();

        Assert.DoesNotContain(
            result,
            x => x.Name == "Alpha");
    }

    [Fact]
    public void ApplyFilter_ShouldApplyStartsWith()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        FilterOperator.StartsWith,
                        "Al"))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            "Alpha",
            item.Name);
    }

    [Fact]
    public void ApplyFilter_ShouldApplyEndsWith()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        FilterOperator.EndsWith,
                        "ta"))
                .ToArray();

        Assert.Contains(
            result,
            x => x.Name == "Beta");

        Assert.Contains(
            result,
            x => x.Name == "Delta");
    }

    [Fact]
    public void ApplyFilter_ShouldApplyIn()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Category), typeof(string)),
                        FilterOperator.In,
                        "A",
                        "C"))
                .ToArray();

        Assert.All(
            result,
            x => Assert.Contains(
                x.Category,
                new[]
                {
                    "A",
                    "C"
                }));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyNotIn()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Category), typeof(string)),
                        FilterOperator.NotIn,
                        "A",
                        "C"))
                .ToArray();

        Assert.All(
            result,
            x => Assert.DoesNotContain(
                x.Category,
                new[]
                {
                    "A",
                    "C"
                }));
    }

    [Fact]
    public void ApplyFilter_ShouldReturnNoRows_WhenInValuesAreEmpty()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Category), typeof(string)),
                        FilterOperator.In))
                .ToArray();

        Assert.Empty(
            result);
    }

    [Fact]
    public void ApplyFilter_ShouldReturnAllRows_WhenNotInValuesAreEmpty()
    {
        var records =
            CreateRecords();

        var result =
            _applier
                .ApplyFilter(
                    records.AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Category), typeof(string)),
                        FilterOperator.NotIn))
                .ToArray();

        Assert.Equal(
            records.Count,
            result.Length);
    }

    [Fact]
    public void ApplyFilter_ShouldApplyBetween()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Amount), typeof(decimal)),
                        FilterOperator.Between,
                        50m,
                        150m))
                .ToArray();

        Assert.All(
            result,
            x => Assert.True(
                x.Amount >= 50m &&
                x.Amount <= 150m));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyNotBetween()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Amount), typeof(decimal)),
                        FilterOperator.NotBetween,
                        50m,
                        150m))
                .ToArray();

        Assert.All(
            result,
            x => Assert.True(
                x.Amount < 50m ||
                x.Amount > 150m));
    }

    [Fact]
    public void ApplyFilter_ShouldThrow_WhenBetweenHasFewerThanTwoValues()
    {
        Assert.Throws<InvalidOperationException>(
            () => _applier.ApplyFilter(
                CreateRecords().AsQueryable(),
                Filter(
                    Field(nameof(TestRecord.Amount), typeof(decimal)),
                    FilterOperator.Between,
                    50m)));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyIsNull()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.NullableCode), typeof(string)),
                        FilterOperator.IsNull))
                .ToArray();

        Assert.All(
            result,
            x => Assert.Null(x.NullableCode));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyIsNotNull()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.NullableCode), typeof(string)),
                        FilterOperator.IsNotNull))
                .ToArray();

        Assert.All(
            result,
            x => Assert.NotNull(x.NullableCode));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyIsTrue()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.IsActive), typeof(bool)),
                        FilterOperator.IsTrue))
                .ToArray();

        Assert.All(
            result,
            x => Assert.True(x.IsActive));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyIsFalse()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.IsActive), typeof(bool)),
                        FilterOperator.IsFalse))
                .ToArray();

        Assert.All(
            result,
            x => Assert.False(x.IsActive));
    }

    [Fact]
    public void ApplyFilter_ShouldApplyAndGroup()
    {
        var filter =
            new CompiledFilterGroup(
                LogicalOperator.And,
                [
                    Filter(
                        Field(nameof(TestRecord.Category), typeof(string)),
                        FilterOperator.Eq,
                        "A"),

                    Filter(
                        Field(nameof(TestRecord.IsActive), typeof(bool)),
                        FilterOperator.IsTrue)
                ]);

        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    filter)
                .ToArray();

        Assert.All(
            result,
            x =>
            {
                Assert.Equal(
                    "A",
                    x.Category);

                Assert.True(
                    x.IsActive);
            });
    }

    [Fact]
    public void ApplyFilter_ShouldApplyOrGroup()
    {
        var filter =
            new CompiledFilterGroup(
                LogicalOperator.Or,
                [
                    Filter(
                        Field(nameof(TestRecord.Category), typeof(string)),
                        FilterOperator.Eq,
                        "A"),

                    Filter(
                        Field(nameof(TestRecord.Category), typeof(string)),
                        FilterOperator.Eq,
                        "C")
                ]);

        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    filter)
                .ToArray();

        Assert.All(
            result,
            x => Assert.Contains(
                x.Category,
                new[]
                {
                    "A",
                    "C"
                }));
    }

    [Fact]
    public void ApplyFilter_ShouldReturnAllRows_WhenGroupIsEmpty()
    {
        var records =
            CreateRecords();

        var result =
            _applier
                .ApplyFilter(
                    records.AsQueryable(),
                    new CompiledFilterGroup(
                        LogicalOperator.And,
                        []))
                .ToArray();

        Assert.Equal(
            records.Count,
            result.Length);
    }

    [Fact]
    public void ApplySearch_ShouldApplyExact()
    {
        var result =
            _applier
                .ApplySearch(
                    CreateRecords().AsQueryable(),
                    Search(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        "Alpha",
                        MatchMode.Exact))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            "Alpha",
            item.Name);
    }

    [Fact]
    public void ApplySearch_ShouldApplyContains()
    {
        var result =
            _applier
                .ApplySearch(
                    CreateRecords().AsQueryable(),
                    Search(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        "ph",
                        MatchMode.Contains))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            "Alpha",
            item.Name);
    }

    [Fact]
    public void ApplySearch_ShouldApplyStartsWith()
    {
        var result =
            _applier
                .ApplySearch(
                    CreateRecords().AsQueryable(),
                    Search(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        "Al",
                        MatchMode.StartsWith))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            "Alpha",
            item.Name);
    }

    [Fact]
    public void ApplySearch_ShouldApplyEndsWith()
    {
        var result =
            _applier
                .ApplySearch(
                    CreateRecords().AsQueryable(),
                    Search(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        "ta",
                        MatchMode.EndsWith))
                .ToArray();

        Assert.Contains(
            result,
            x => x.Name == "Beta");

        Assert.Contains(
            result,
            x => x.Name == "Delta");
    }

    [Fact]
    public void ApplySearch_ShouldApplyOrGroup()
    {
        var search =
            new CompiledSearchGroup(
                LogicalOperator.Or,
                [
                    Search(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        "Alpha",
                        MatchMode.Exact),

                    Search(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        "Beta",
                        MatchMode.Exact)
                ]);

        var result =
            _applier
                .ApplySearch(
                    CreateRecords().AsQueryable(),
                    search)
                .ToArray();

        Assert.Equal(
            2,
            result.Length);

        Assert.Contains(
            result,
            x => x.Name == "Alpha");

        Assert.Contains(
            result,
            x => x.Name == "Beta");
    }

    [Fact]
    public void ApplySearch_ShouldApplyAndGroup()
    {
        var search =
            new CompiledSearchGroup(
                LogicalOperator.And,
                [
                    Search(
                        Field(nameof(TestRecord.Name), typeof(string)),
                        "a",
                        MatchMode.Contains),

                    Search(
                        Field(nameof(TestRecord.Category), typeof(string)),
                        "A",
                        MatchMode.Exact)
                ]);

        var result =
            _applier
                .ApplySearch(
                    CreateRecords().AsQueryable(),
                    search)
                .ToArray();

        Assert.All(
            result,
            x =>
            {
                Assert.Contains(
                    "a",
                    x.Name,
                    StringComparison.Ordinal);

                Assert.Equal(
                    "A",
                    x.Category);
            });
    }

    [Fact]
    public void ApplySearch_ShouldThrow_WhenFuzzyIsUsed()
    {
        Assert.Throws<NotSupportedException>(
            () => _applier.ApplySearch(
                CreateRecords().AsQueryable(),
                Search(
                    Field(nameof(TestRecord.Name), typeof(string)),
                    "Alpha",
                    MatchMode.Fuzzy)));
    }

    [Fact]
    public void ApplySearch_ShouldThrow_WhenSoundexIsUsed()
    {
        Assert.Throws<NotSupportedException>(
            () => _applier.ApplySearch(
                CreateRecords().AsQueryable(),
                Search(
                    Field(nameof(TestRecord.Name), typeof(string)),
                    "Alpha",
                    MatchMode.Soundex)));
    }

    [Fact]
    public void ApplySearch_ShouldThrow_WhenFullTextIsUsed()
    {
        Assert.Throws<NotSupportedException>(
            () => _applier.ApplySearch(
                CreateRecords().AsQueryable(),
                Search(
                    Field(nameof(TestRecord.Name), typeof(string)),
                    "Alpha",
                    MatchMode.FullText)));
    }

    [Fact]
    public void ApplyFilter_ShouldThrow_WhenStringOperatorIsAppliedToNonStringField()
    {
        Assert.Throws<NotSupportedException>(
            () => _applier.ApplyFilter(
                CreateRecords().AsQueryable(),
                Filter(
                    Field(nameof(TestRecord.Amount), typeof(decimal)),
                    FilterOperator.Contains,
                    "1")));
    }

    [Fact]
    public void ApplyFilter_ShouldThrow_WhenBooleanOperatorIsAppliedToNonBooleanField()
    {
        Assert.Throws<NotSupportedException>(
            () => _applier.ApplyFilter(
                CreateRecords().AsQueryable(),
                Filter(
                    Field(nameof(TestRecord.Amount), typeof(decimal)),
                    FilterOperator.IsTrue)));
    }

    [Fact]
    public void ApplySort_ShouldOrderAscending()
    {
        var result =
            _applier
                .ApplySort(
                    CreateRecords().AsQueryable(),
                    [
                        new CompiledSort(
                            Field(nameof(TestRecord.Amount), typeof(decimal)),
                            SortDirection.Ascending,
                            0)
                    ])
                .ToArray();

        Assert.Equal(
            new[]
            {
                25m,
                75m,
                100m,
                150m,
                250m
            },
            result.Select(x => x.Amount).ToArray());
    }

    [Fact]
    public void ApplySort_ShouldOrderDescending()
    {
        var result =
            _applier
                .ApplySort(
                    CreateRecords().AsQueryable(),
                    [
                        new CompiledSort(
                            Field(nameof(TestRecord.Amount), typeof(decimal)),
                            SortDirection.Descending,
                            0)
                    ])
                .ToArray();

        Assert.Equal(
            new[]
            {
                250m,
                150m,
                100m,
                75m,
                25m
            },
            result.Select(x => x.Amount).ToArray());
    }

    [Fact]
    public void ApplySort_ShouldApplyThenBy()
    {
        var result =
            _applier
                .ApplySort(
                    CreateRecords().AsQueryable(),
                    [
                        new CompiledSort(
                            Field(nameof(TestRecord.Category), typeof(string)),
                            SortDirection.Ascending,
                            0),

                        new CompiledSort(
                            Field(nameof(TestRecord.Amount), typeof(decimal)),
                            SortDirection.Descending,
                            1)
                    ])
                .ToArray();

        Assert.Equal(
            new[]
            {
                "Gamma",
                "Alpha",
                "Delta",
                "Beta",
                "Epsilon"
            },
            result.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void ApplySort_ShouldOrderBySequence()
    {
        var result =
            _applier
                .ApplySort(
                    CreateRecords().AsQueryable(),
                    [
                        new CompiledSort(
                            Field(nameof(TestRecord.Amount), typeof(decimal)),
                            SortDirection.Ascending,
                            1),

                        new CompiledSort(
                            Field(nameof(TestRecord.Category), typeof(string)),
                            SortDirection.Ascending,
                            0)
                    ])
                .ToArray();

        Assert.Equal(
            new[]
            {
                "Alpha",
                "Gamma",
                "Beta",
                "Delta",
                "Epsilon"
            },
            result.Select(x => x.Name).ToArray());
    }

    [Fact]
    public void ApplySort_ShouldSortEnumsByDescription()
    {
        var result =
            _applier
                .ApplySort(
                    CreateRecords().AsQueryable(),
                    [
                        new CompiledSort(
                            Field(nameof(TestRecord.Status), typeof(TestStatus)),
                            SortDirection.Ascending,
                            0)
                    ])
                .ToArray();

        Assert.Equal(
            new[]
            {
                TestStatus.Active,
                TestStatus.Pending,
                TestStatus.Unknown,
                TestStatus.Unknown,
                TestStatus.Retired
            },
            result.Select(x => x.Status).ToArray());
    }

    [Fact]
    public void ApplyPage_ShouldApplySkipAndTake()
    {
        var result =
            _applier
                .ApplyPage(
                    CreateRecords()
                        .OrderBy(x => x.Id)
                        .AsQueryable(),
                    new CompiledPage(
                        2,
                        1))
                .ToArray();

        Assert.Equal(
            new[]
            {
                2,
                3
            },
            result.Select(x => x.Id).ToArray());
    }

    [Fact]
    public void ApplyFilter_ShouldConvertStringValueToInt()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Id), typeof(int)),
                        FilterOperator.Eq,
                        "1"))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            1,
            item.Id);
    }

    [Fact]
    public void ApplyFilter_ShouldConvertStringValueToDateOnly()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.EffectiveDate), typeof(DateOnly)),
                        FilterOperator.Eq,
                        "2026-01-02"))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            new DateOnly(2026, 1, 2),
            item.EffectiveDate);
    }

    [Fact]
    public void ApplyFilter_ShouldConvertStringValueToGuid()
    {
        var id =
            CreateRecords()[0].ExternalId;

        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.ExternalId), typeof(Guid)),
                        FilterOperator.Eq,
                        id.ToString()))
                .ToArray();

        var item =
            Assert.Single(result);

        Assert.Equal(
            id,
            item.ExternalId);
    }

    [Fact]
    public void ApplyFilter_ShouldConvertStringValueToEnum()
    {
        var result =
            _applier
                .ApplyFilter(
                    CreateRecords().AsQueryable(),
                    Filter(
                        Field(nameof(TestRecord.Status), typeof(TestStatus)),
                        FilterOperator.Eq,
                        "Active"))
                .ToArray();

        Assert.All(
            result,
            x => Assert.Equal(
                TestStatus.Active,
                x.Status));
    }

    private static CompiledFilterCondition Filter(
        FieldMetadata field,
        FilterOperator filterOperator,
        params object?[] values)
    {
        return new CompiledFilterCondition(
            field,
            filterOperator,
            values);
    }

    private static CompiledSearchCondition Search(
        FieldMetadata field,
        string searchText,
        MatchMode matchMode)
    {
        return new CompiledSearchCondition(
            field,
            searchText,
            matchMode);
    }

    private static FieldMetadata Field(
        string name,
        Type type)
    {
        return new FieldMetadata(
            name,
            type,
            true,
            [],
            true,
            null,
            [],
            true);
    }

    private static List<TestRecord> CreateRecords()
    {
        return
        [
            new TestRecord(
                1,
                "Alpha",
                "A",
                100m,
                true,
                "A1",
                TestStatus.Active,
                new DateOnly(2026, 1, 1),
                Guid.Parse("11111111-1111-1111-1111-111111111111")),

            new TestRecord(
                2,
                "Beta",
                "B",
                75m,
                false,
                null,
                TestStatus.Pending,
                new DateOnly(2026, 1, 2),
                Guid.Parse("22222222-2222-2222-2222-222222222222")),

            new TestRecord(
                3,
                "Gamma",
                "A",
                250m,
                true,
                "G1",
                TestStatus.Retired,
                new DateOnly(2026, 1, 3),
                Guid.Parse("33333333-3333-3333-3333-333333333333")),

            new TestRecord(
                4,
                "Delta",
                "B",
                150m,
                true,
                null,
                TestStatus.Unknown,
                new DateOnly(2026, 1, 4),
                Guid.Parse("44444444-4444-4444-4444-444444444444")),

            new TestRecord(
                5,
                "Epsilon",
                "C",
                25m,
                false,
                "E1",
                TestStatus.Unknown,
                new DateOnly(2026, 1, 5),
                Guid.Parse("55555555-5555-5555-5555-555555555555"))
        ];
    }

    private sealed record TestRecord(
        int Id,
        string Name,
        string Category,
        decimal Amount,
        bool IsActive,
        string? NullableCode,
        TestStatus Status,
        DateOnly EffectiveDate,
        Guid ExternalId);

    private enum TestStatus
    {
        [Description("Delta")]
        Unknown,

        [Description("Alpha")]
        Active,

        [Description("Beta")]
        Pending,

        [Description("Gamma")]
        Retired
    }
}