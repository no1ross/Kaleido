namespace Kaleido.UnitTests.Queryable;

public sealed class QueryableCompiledQueryApplierTests
{
    private readonly QueryableCompiledQueryApplier<TestRecord> _sut;

    public QueryableCompiledQueryApplierTests()
    {
        _sut = new QueryableCompiledQueryApplier<TestRecord>();
    }

    [Fact]
    public void ApplyFilter_Should_Return_Original_Query_When_Filter_Is_Null()
    {
        var query = TestData.Records().AsQueryable();

        var result = _sut.ApplyFilter(query, null);

        Assert.Same(query, result);
    }

    [Fact]
    public void ApplySearch_Should_Return_Original_Query_When_Search_Is_Null()
    {
        var query = TestData.Records().AsQueryable();

        var result = _sut.ApplySearch(query, null);

        Assert.Same(query, result);
    }

    [Fact]
    public void ApplyPage_Should_Use_Offset_And_Size()
    {
        var query = TestData.Records().AsQueryable();

        var result = _sut
            .ApplyPage(query, new CompiledPage(Offset: 2, Size: 3))
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([3, 4, 5], result);
    }

    [Fact]
    public void ApplySort_Should_Sort_Ascending()
    {
        var query = TestData.Records().AsQueryable();

        var result = _sut
            .ApplySort(
                query,
                [
                    new CompiledSort(
                        TestData.Field(nameof(TestRecord.Name), typeof(string)),
                        SortDirection.Ascending,
                        Sequence: 1)
                ])
            .Select(x => x.Name)
            .ToArray();

        Assert.Equal(
            ["Alpha", "Beta", "Delta", "Echo", "Gamma", "Zeta"],
            result);
    }

    [Fact]
    public void ApplySort_Should_Sort_Descending()
    {
        var query = TestData.Records().AsQueryable();

        var result = _sut
            .ApplySort(
                query,
                [
                    new CompiledSort(
                        TestData.Field(nameof(TestRecord.Name), typeof(string)),
                        SortDirection.Descending,
                        Sequence: 1)
                ])
            .Select(x => x.Name)
            .ToArray();

        Assert.Equal(
            ["Zeta", "Gamma", "Echo", "Delta", "Beta", "Alpha"],
            result);
    }

    [Fact]
    public void ApplySort_Should_Apply_ThenBy_Using_Sequence()
    {
        var query = TestData.Records().AsQueryable();

        var result = _sut
            .ApplySort(
                query,
                [
                    new CompiledSort(
                        TestData.Field(nameof(TestRecord.Category), typeof(string)),
                        SortDirection.Ascending,
                        Sequence: 1),

                    new CompiledSort(
                        TestData.Field(nameof(TestRecord.Id), typeof(int)),
                        SortDirection.Descending,
                        Sequence: 2)
                ])
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([6, 4, 2, 5, 3, 1], result);
    }

    [Fact]
    public void ApplySort_Should_Sort_Enum_By_Description()
    {
        var query = TestData.Records().AsQueryable();

        var result = _sut
            .ApplySort(
                query,
                [
                    new CompiledSort(
                        TestData.Field(nameof(TestRecord.Status), typeof(TestStatus)),
                        SortDirection.Ascending,
                        Sequence: 1),

                    new CompiledSort(
                        TestData.Field(nameof(TestRecord.Id), typeof(int)),
                        SortDirection.Ascending,
                        Sequence: 2)
                ])
            .Select(x => x.Status)
            .ToArray();

        Assert.Equal(
            [
                TestStatus.Active,
                TestStatus.Archived,
                TestStatus.Draft,
                TestStatus.Pending,
                TestStatus.Suspended,
                TestStatus.Unknown
            ],
            result);
    }

    [Fact]
    public void ApplyFilter_Eq_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Category),
            typeof(string),
            FilterOperator.Eq,
            "A");

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([2, 4, 6], result);
    }

    [Fact]
    public void ApplyFilter_Ne_Should_Return_NonMatching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Category),
            typeof(string),
            FilterOperator.Ne,
            "A");

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 3, 5], result);
    }

    [Fact]
    public void ApplyFilter_Gt_Should_Return_Greater_Than_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.Gt,
            30);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([4, 5, 6], result);
    }

    [Fact]
    public void ApplyFilter_Gte_Should_Return_Greater_Than_Or_Equal_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.Gte,
            30);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([3, 4, 5, 6], result);
    }

    [Fact]
    public void ApplyFilter_Lt_Should_Return_Less_Than_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.Lt,
            30);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 2], result);
    }

    [Fact]
    public void ApplyFilter_Lte_Should_Return_Less_Than_Or_Equal_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.Lte,
            30);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 2, 3], result);
    }

    [Fact]
    public void ApplyFilter_Contains_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Name),
            typeof(string),
            FilterOperator.Contains,
            "ta");

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([2, 4, 6], result);
    }

    [Fact]
    public void ApplyFilter_NotContains_Should_Return_NonMatching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Name),
            typeof(string),
            FilterOperator.NotContains,
            "ta");

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 3, 5], result);
    }

    [Fact]
    public void ApplyFilter_StartsWith_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Code),
            typeof(string),
            FilterOperator.StartsWith,
            "A");

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 3, 6], result);
    }

    [Fact]
    public void ApplyFilter_EndsWith_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Code),
            typeof(string),
            FilterOperator.EndsWith,
            "01");

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1], result);
    }

    [Fact]
    public void ApplyFilter_In_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Id),
            typeof(int),
            FilterOperator.In,
            1,
            3,
            5);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 3, 5], result);
    }

    [Fact]
    public void ApplyFilter_NotIn_Should_Return_NonMatching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Id),
            typeof(int),
            FilterOperator.NotIn,
            1,
            3,
            5);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([2, 4, 6], result);
    }

    [Fact]
    public void ApplyFilter_In_With_Empty_Values_Should_Return_No_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Id),
            typeof(int),
            FilterOperator.In);

        var result = _sut
            .ApplyFilter(query, filter)
            .ToArray();

        Assert.Empty(result);
    }

    [Fact]
    public void ApplyFilter_NotIn_With_Empty_Values_Should_Return_All_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Id),
            typeof(int),
            FilterOperator.NotIn);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 2, 3, 4, 5, 6], result);
    }

    [Fact]
    public void ApplyFilter_Between_Should_Return_Records_Inclusive()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.Between,
            20,
            40);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([2, 3, 4], result);
    }

    [Fact]
    public void ApplyFilter_NotBetween_Should_Return_Records_Outside_Range()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.NotBetween,
            20,
            40);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 5, 6], result);
    }

    [Fact]
    public void ApplyFilter_IsNull_Should_Return_Null_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.ExpirationDate),
            typeof(DateOnly?),
            FilterOperator.IsNull);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([2, 4, 6], result);
    }

    [Fact]
    public void ApplyFilter_IsNotNull_Should_Return_NonNull_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.ExpirationDate),
            typeof(DateOnly?),
            FilterOperator.IsNotNull);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 3, 5], result);
    }

    [Fact]
    public void ApplyFilter_IsTrue_Should_Return_True_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.IsActive),
            typeof(bool),
            FilterOperator.IsTrue);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 3, 5], result);
    }

    [Fact]
    public void ApplyFilter_IsFalse_Should_Return_False_Records()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.IsActive),
            typeof(bool),
            FilterOperator.IsFalse);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([2, 4, 6], result);
    }

    [Fact]
    public void ApplyFilter_Group_And_Should_Return_Records_Matching_All_Conditions()
    {
        var query = TestData.Records().AsQueryable();

        var filter = new CompiledFilterGroup(
            LogicalOperator.And,
            [
                TestData.Filter(
                    nameof(TestRecord.Category),
                    typeof(string),
                    FilterOperator.Eq,
                    "A"),

                TestData.Filter(
                    nameof(TestRecord.IsActive),
                    typeof(bool),
                    FilterOperator.IsFalse)
            ]);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([2, 4, 6], result);
    }

    [Fact]
    public void ApplyFilter_Group_Or_Should_Return_Records_Matching_Any_Condition()
    {
        var query = TestData.Records().AsQueryable();

        var filter = new CompiledFilterGroup(
            LogicalOperator.Or,
            [
                TestData.Filter(
                    nameof(TestRecord.Id),
                    typeof(int),
                    FilterOperator.Eq,
                    1),

                TestData.Filter(
                    nameof(TestRecord.Id),
                    typeof(int),
                    FilterOperator.Eq,
                    6)
            ]);

        var result = _sut
            .ApplyFilter(query, filter)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 6], result);
    }

    [Fact]
    public void ApplySearch_Exact_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var search = TestData.Search(
            nameof(TestRecord.Name),
            typeof(string),
            MatchMode.Exact,
            "Alpha");

        var result = _sut
            .ApplySearch(query, search)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1], result);
    }

    [Fact]
    public void ApplySearch_StartsWith_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var search = TestData.Search(
            nameof(TestRecord.Name),
            typeof(string),
            MatchMode.StartsWith,
            "D");

        var result = _sut
            .ApplySearch(query, search)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([4], result);
    }

    [Fact]
    public void ApplySearch_EndsWith_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var search = TestData.Search(
            nameof(TestRecord.Name),
            typeof(string),
            MatchMode.EndsWith,
            "ta");

        var result = _sut
            .ApplySearch(query, search)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([2, 4, 6], result);
    }

    [Fact]
    public void ApplySearch_Contains_Should_Return_Matching_Records()
    {
        var query = TestData.Records().AsQueryable();

        var search = TestData.Search(
            nameof(TestRecord.Name),
            typeof(string),
            MatchMode.Contains,
            "a");

        var result = _sut
            .ApplySearch(query, search)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 2, 3, 4, 6], result);
    }

    [Fact]
    public void ApplySearch_Fuzzy_Should_Throw_NotSupportedException()
    {
        var query = TestData.Records().AsQueryable();

        var search = TestData.Search(
            nameof(TestRecord.Name),
            typeof(string),
            MatchMode.Fuzzy,
            "Alpha");

        Assert.Throws<NotSupportedException>(
            () => _sut.ApplySearch(query, search).ToArray());
    }

    [Fact]
    public void ApplySearch_Soundex_Should_Throw_NotSupportedException()
    {
        var query = TestData.Records().AsQueryable();

        var search = TestData.Search(
            nameof(TestRecord.Name),
            typeof(string),
            MatchMode.Soundex,
            "Alpha");

        Assert.Throws<NotSupportedException>(
            () => _sut.ApplySearch(query, search).ToArray());
    }

    [Fact]
    public void ApplySearch_FullText_Should_Throw_NotSupportedException()
    {
        var query = TestData.Records().AsQueryable();

        var search = TestData.Search(
            nameof(TestRecord.Name),
            typeof(string),
            MatchMode.FullText,
            "Alpha");

        Assert.Throws<NotSupportedException>(
            () => _sut.ApplySearch(query, search).ToArray());
    }

    [Fact]
    public void ApplySearch_Group_And_Should_Return_Records_Matching_All_Conditions()
    {
        var query = TestData.Records().AsQueryable();

        var search = new CompiledSearchGroup(
            LogicalOperator.And,
            [
                TestData.Search(
                    nameof(TestRecord.Name),
                    typeof(string),
                    MatchMode.Contains,
                    "a"),

                TestData.Search(
                    nameof(TestRecord.Code),
                    typeof(string),
                    MatchMode.StartsWith,
                    "A")
            ]);

        var result = _sut
            .ApplySearch(query, search)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 3, 6], result);
    }

    [Fact]
    public void ApplySearch_Group_Or_Should_Return_Records_Matching_Any_Condition()
    {
        var query = TestData.Records().AsQueryable();

        var search = new CompiledSearchGroup(
            LogicalOperator.Or,
            [
                TestData.Search(
                    nameof(TestRecord.Name),
                    typeof(string),
                    MatchMode.Exact,
                    "Alpha"),

                TestData.Search(
                    nameof(TestRecord.Name),
                    typeof(string),
                    MatchMode.Exact,
                    "Zeta")
            ]);

        var result = _sut
            .ApplySearch(query, search)
            .Select(x => x.Id)
            .ToArray();

        Assert.Equal([1, 6], result);
    }

    [Fact]
    public void ApplyFilter_StringOperator_On_NonString_Field_Should_Throw()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.Contains,
            "1");

        Assert.Throws<NotSupportedException>(
            () => _sut.ApplyFilter(query, filter).ToArray());
    }

    [Fact]
    public void ApplyFilter_Between_With_One_Value_Should_Throw()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.Between,
            10);

        Assert.Throws<InvalidOperationException>(
            () => _sut.ApplyFilter(query, filter).ToArray());
    }

    [Fact]
    public void ApplyFilter_IsTrue_On_NonBoolean_Field_Should_Throw()
    {
        var query = TestData.Records().AsQueryable();

        var filter = TestData.Filter(
            nameof(TestRecord.Quantity),
            typeof(int),
            FilterOperator.IsTrue);

        Assert.Throws<NotSupportedException>(
            () => _sut.ApplyFilter(query, filter).ToArray());
    }

    private static class TestData
    {
        public static IReadOnlyList<TestRecord> Records()
        {
            return
            [
                new TestRecord
                {
                    Id = 1,
                    Name = "Alpha",
                    Code = "A-001",
                    Category = "B",
                    Quantity = 10,
                    Amount = 10.5m,
                    IsActive = true,
                    ExpirationDate = new DateOnly(2024, 1, 1),
                    Status = TestStatus.Unknown
                },
                new TestRecord
                {
                    Id = 2,
                    Name = "Beta",
                    Code = "B-002",
                    Category = "A",
                    Quantity = 20,
                    Amount = 20.5m,
                    IsActive = false,
                    ExpirationDate = null,
                    Status = TestStatus.Draft
                },
                new TestRecord
                {
                    Id = 3,
                    Name = "Gamma",
                    Code = "A-003",
                    Category = "B",
                    Quantity = 30,
                    Amount = 30.5m,
                    IsActive = true,
                    ExpirationDate = new DateOnly(2024, 3, 1),
                    Status = TestStatus.Pending
                },
                new TestRecord
                {
                    Id = 4,
                    Name = "Delta",
                    Code = "D-004",
                    Category = "A",
                    Quantity = 40,
                    Amount = 40.5m,
                    IsActive = false,
                    ExpirationDate = null,
                    Status = TestStatus.Suspended
                },
                new TestRecord
                {
                    Id = 5,
                    Name = "Echo",
                    Code = "E-005",
                    Category = "B",
                    Quantity = 50,
                    Amount = 50.5m,
                    IsActive = true,
                    ExpirationDate = new DateOnly(2024, 5, 1),
                    Status = TestStatus.Archived
                },
                new TestRecord
                {
                    Id = 6,
                    Name = "Zeta",
                    Code = "A-006",
                    Category = "A",
                    Quantity = 60,
                    Amount = 60.5m,
                    IsActive = false,
                    ExpirationDate = null,
                    Status = TestStatus.Active
                }
            ];
        }

        public static RuntimeFieldMetadata Field(
            string name,
            Type fieldType)
        {
            return new RuntimeFieldMetadata(
                Name: name,
                FieldType: fieldType,
                IsFilterable: true,
                FilterOperators: [],
                IsSearchable: true,
                SearchPriority: 1,
                MatchModes: [],
                IsSortable: true);
        }

        public static CompiledFilterCondition Filter(
            string field,
            Type fieldType,
            FilterOperator op,
            params object?[] values)
        {
            return new CompiledFilterCondition(
                Field(field, fieldType),
                op,
                values);
        }

        public static CompiledSearchCondition Search(
            string field,
            Type fieldType,
            MatchMode mode,
            string text)
        {
            return new CompiledSearchCondition(
                Field(field, fieldType),
                text,
                mode);
        }
    }

    private sealed class TestRecord
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public string Category { get; init; } = string.Empty;

        public int Quantity { get; init; }

        public decimal Amount { get; init; }

        public bool IsActive { get; init; }

        public DateOnly? ExpirationDate { get; init; }

        public TestStatus Status { get; init; }
    }

    private enum TestStatus
    {
        [System.ComponentModel.Description("Unknown")]
        Unknown = 0,

        [System.ComponentModel.Description("Draft")]
        Draft = 1,

        [System.ComponentModel.Description("Pending")]
        Pending = 2,

        [System.ComponentModel.Description("Suspended")]
        Suspended = 3,

        [System.ComponentModel.Description("Archived")]
        Archived = 4,

        [System.ComponentModel.Description("Active")]
        Active = 5
    }
}