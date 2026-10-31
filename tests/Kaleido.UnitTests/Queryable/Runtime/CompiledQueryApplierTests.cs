using Kaleido.Exceptions;

namespace Kaleido.Queryable.Runtime.UnitTests;

public sealed class CompiledQueryApplierTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly DataTypeDescriptor TestDataType =
        new("string");

    public enum FakeStatus
    {
        Active,
        Inactive,
    }

    private sealed class FakeRecord
    {
        public string? Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public int? Score { get; set; }
        public long Count { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }
        public bool? Verified { get; set; }
        public FakeStatus Status { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    private sealed record UnknownFilter : CompiledFilterExpression;

    private static CompiledQueryApplier<FakeRecord> CreateSut() => new();

    private static readonly FakeRecord[] Records =
    [
        new() { Name = "Alpha", Age = 10, Score = 1, Count = 100, Price = 1.5m, Active = true, Verified = true, Status = FakeStatus.Active },
        new() { Name = "beta", Age = 20, Score = null, Count = 200, Price = 2.5m, Active = false, Verified = null, Status = FakeStatus.Inactive },
        new() { Name = null, Age = 30, Score = 3, Count = 300, Price = 3.5m, Active = true, Verified = false, Status = FakeStatus.Active },
    ];

    [Fact]
    public void ApplyFilter_WhenFilterIsNull_ReturnsUnmodifiedQuery()
    {
        var sut = CreateSut();
        var source = new[] { new FakeRecord { Name = "alpha" } }.AsQueryable();

        var result = sut.ApplyFilter(source, filter: null);

        Assert.Equal(source, result);
    }

    [Fact]
    public void ApplySearch_WhenSearchIsNull_ReturnsUnmodifiedQuery()
    {
        var sut = CreateSut();
        var source = new[] { new FakeRecord { Name = "alpha" } }.AsQueryable();

        var result = sut.ApplySearch(source, search: null);

        Assert.Equal(source, result);
    }

    [Fact]
    public void ApplySort_WhenSortIsEmpty_ReturnsUnmodifiedQuery()
    {
        var sut = CreateSut();
        var source = new[] { new FakeRecord { Name = "alpha" } }.AsQueryable();

        var result = sut.ApplySort(source, sort: []);

        Assert.Equal(source, result);
    }

    [Theory]
    [InlineData(FilterOperator.Equals, "ALPHA", new[] { 10 })]
    [InlineData(FilterOperator.NotEquals, "alpha", new[] { 20, 30 })]
    [InlineData(FilterOperator.Contains, "ET", new[] { 20 })]
    [InlineData(FilterOperator.NotContains, "et", new[] { 10, 30 })]
    [InlineData(FilterOperator.StartsWith, "AL", new[] { 10 })]
    [InlineData(FilterOperator.EndsWith, "TA", new[] { 20 })]
    public void ApplyFilter_StringOperators_AreCaseInsensitiveAndNullSafe(FilterOperator op, string value, int[] expectedAges)
    {
        var result = Filter(Condition(nameof(FakeRecord.Name), op, value));

        Assert.Equal(expectedAges, result);
    }

    [Fact]
    public void ApplyFilter_StringEqualsNull_MatchesNullMembers()
    {
        Assert.Equal([30], Filter(Condition(nameof(FakeRecord.Name), FilterOperator.Equals, [null])));
        Assert.Equal([10, 20], Filter(Condition(nameof(FakeRecord.Name), FilterOperator.NotEquals, [null])));
    }

    [Fact]
    public void ApplyFilter_StringOperatorWithNullValue_UsesEmptyString()
    {
        Assert.Equal([10, 20], Filter(Condition(nameof(FakeRecord.Name), FilterOperator.Contains, [null])));
    }

    [Theory]
    [InlineData(FilterOperator.Equals, 20, new[] { 20 })]
    [InlineData(FilterOperator.NotEquals, 20, new[] { 10, 30 })]
    [InlineData(FilterOperator.GreaterThan, 20, new[] { 30 })]
    [InlineData(FilterOperator.GreaterThanOrEqual, 20, new[] { 20, 30 })]
    [InlineData(FilterOperator.LessThan, 20, new[] { 10 })]
    [InlineData(FilterOperator.LessThanOrEqual, 20, new[] { 10, 20 })]
    public void ApplyFilter_ComparisonOperators_FilterValueTypes(FilterOperator op, int value, int[] expectedAges)
    {
        Assert.Equal(expectedAges, Filter(Condition(nameof(FakeRecord.Age), op, value)));
    }

    [Fact]
    public void ApplyFilter_NullableMember_ConvertsValueAndSupportsNull()
    {
        Assert.Equal([30], Filter(Condition(nameof(FakeRecord.Score), FilterOperator.GreaterThan, 1)));
        Assert.Equal([20], Filter(Condition(nameof(FakeRecord.Score), FilterOperator.Equals, [null])));
    }

    [Fact]
    public void ApplyFilter_NumericValueOfAnotherType_IsConverted()
    {
        Assert.Equal([20], Filter(Condition(nameof(FakeRecord.Age), FilterOperator.Equals, 20L)));
        Assert.Equal([30], Filter(Condition(nameof(FakeRecord.Count), FilterOperator.Equals, 300)));
    }

    [Theory]
    [InlineData((byte)2)]
    [InlineData((sbyte)2)]
    [InlineData((short)2)]
    [InlineData((ushort)2)]
    [InlineData(2)]
    [InlineData(2u)]
    [InlineData(2L)]
    [InlineData(2ul)]
    [InlineData(2f)]
    [InlineData(2d)]
    public void ApplyFilter_EveryNumericType_IsConvertedToTheMemberType(object value)
    {
        Assert.Equal([20, 30], Filter(Condition(nameof(FakeRecord.Price), FilterOperator.GreaterThan, value)));
    }

    [Fact]
    public void ApplyFilter_DecimalValueForIntMember_IsConverted()
    {
        Assert.Equal([10], Filter(Condition(nameof(FakeRecord.Age), FilterOperator.Equals, 10m)));
    }

    [Fact]
    public void ApplyFilter_EnumMemberWithStringValue_ParsesIgnoringCase()
    {
        Assert.Equal([20], Filter(Condition(nameof(FakeRecord.Status), FilterOperator.Equals, "inactive")));
        Assert.Equal([10, 30], Filter(Condition(nameof(FakeRecord.Status), FilterOperator.Equals, FakeStatus.Active)));
    }

    [Fact]
    public void ApplyFilter_EnumMemberWithNumericValue_ThrowsInvalidFilterValue()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.Status), FilterOperator.Equals, 1)));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
    }

    [Fact]
    public void ApplyFilter_NonNumericValueForNumericMember_ThrowsInvalidFilterValue()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.Age), FilterOperator.Equals, "abc")));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
        Assert.Contains("Int32", exception.Message);
    }

    [Fact]
    public void ApplyFilter_IncompatibleValueForNonNumericMember_ThrowsInvalidFilterValue()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.CreatedOn), FilterOperator.Equals, "2024-01-01")));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);

        // #520: the expected type is named, the value never is.
        Assert.Contains("'String'", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("2024-01-01", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyFilter_NullValueForNonNullableMember_ThrowsInvalidFilterValue()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.Age), FilterOperator.Equals, [null])));

        Assert.Equal(QueryableErrorCodes.InvalidFilterValue, exception.Code);
        Assert.Equal("A null filter value is not valid here. Expected a value of type 'Int32'.", exception.Message);
    }

    [Fact]
    public void ApplyFilter_StringIn_IsCaseInsensitiveAndNullSafe()
    {
        Assert.Equal([10, 20], Filter(Condition(nameof(FakeRecord.Name), FilterOperator.In, "ALPHA", "Beta", null)));
        Assert.Equal([20, 30], Filter(Condition(nameof(FakeRecord.Name), FilterOperator.NotIn, "alpha")));
    }

    [Fact]
    public void ApplyFilter_ValueTypeIn_UsesTypedArrayWithConversion()
    {
        Assert.Equal([10, 30], Filter(Condition(nameof(FakeRecord.Age), FilterOperator.In, 10, 30L)));
        Assert.Equal([20], Filter(Condition(nameof(FakeRecord.Age), FilterOperator.NotIn, 10, 30)));
        Assert.Equal([20], Filter(Condition(nameof(FakeRecord.Status), FilterOperator.In, "INACTIVE")));
    }

    [Fact]
    public void ApplyFilter_InWithoutValues_MatchesNothingAndNotInMatchesEverything()
    {
        Assert.Empty(Filter(Condition(nameof(FakeRecord.Age), FilterOperator.In)));
        Assert.Equal([10, 20, 30], Filter(Condition(nameof(FakeRecord.Age), FilterOperator.NotIn)));
    }

    [Fact]
    public void ApplyFilter_Between_IsInclusive()
    {
        Assert.Equal([10, 20], Filter(Condition(nameof(FakeRecord.Age), FilterOperator.Between, 10, 20)));
        Assert.Equal([30], Filter(Condition(nameof(FakeRecord.Age), FilterOperator.NotBetween, 10, 20)));
    }

    [Theory]
    [InlineData(FilterOperator.Between)]
    [InlineData(FilterOperator.NotBetween)]
    public void ApplyFilter_BetweenWithFewerThanTwoValues_ThrowsInvalidFilterNode(FilterOperator op)
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.Age), op, 10)));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, exception.Code);
    }

    [Fact]
    public void ApplyFilter_NullChecks_FilterNullMembers()
    {
        Assert.Equal([20], Filter(Condition(nameof(FakeRecord.Score), FilterOperator.IsNull)));
        Assert.Equal([10, 20], Filter(Condition(nameof(FakeRecord.Name), FilterOperator.IsNotNull)));
    }

    [Fact]
    public void ApplyFilter_BooleanOperators_SupportBoolAndNullableBool()
    {
        Assert.Equal([10, 30], Filter(Condition(nameof(FakeRecord.Active), FilterOperator.IsTrue)));
        Assert.Equal([20], Filter(Condition(nameof(FakeRecord.Active), FilterOperator.IsFalse)));
        Assert.Equal([10], Filter(Condition(nameof(FakeRecord.Verified), FilterOperator.IsTrue)));
        Assert.Equal([30], Filter(Condition(nameof(FakeRecord.Verified), FilterOperator.IsFalse)));
    }

    [Fact]
    public void ApplyFilter_BooleanOperatorOnNonBoolMember_ThrowsInvalidFilterNode()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.Name), FilterOperator.IsTrue)));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, exception.Code);
    }

    [Fact]
    public void ApplyFilter_StringOperatorOnNonStringMember_ThrowsInvalidFilterNode()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.Age), FilterOperator.Contains, "1")));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, exception.Code);
        Assert.Contains("Int32", exception.Message);
    }

    [Fact]
    public void ApplyFilter_OperatorWithoutValue_ThrowsInvalidFilterNode()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.Age), FilterOperator.Equals)));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, exception.Code);
        Assert.Contains("index 0", exception.Message);
    }

    [Fact]
    public void ApplyFilter_UnknownOperator_ThrowsUnsupportedOperator()
    {
        var exception = Assert.Throws<KaleidoValidationException>(
            () => Filter(Condition(nameof(FakeRecord.Age), (FilterOperator)999, 1)));

        Assert.Equal(QueryableErrorCodes.UnsupportedOperator, exception.Code);
    }

    [Fact]
    public void ApplyFilter_UnknownFilterNode_ThrowsInvalidFilterNode()
    {
        var exception = Assert.Throws<KaleidoValidationException>(() => Filter(new UnknownFilter()));

        Assert.Equal(QueryableErrorCodes.InvalidFilterNode, exception.Code);
        Assert.Contains(nameof(UnknownFilter), exception.Message);
    }

    [Fact]
    public void ApplyFilter_Groups_CombineWithAndOrAndNest()
    {
        var and = new CompiledFilterGroup(
            LogicalOperator.And,
            [
                Condition(nameof(FakeRecord.Active), FilterOperator.IsTrue),
                Condition(nameof(FakeRecord.Age), FilterOperator.GreaterThan, 10),
            ]);
        var or = new CompiledFilterGroup(
            LogicalOperator.Or,
            [
                and,
                Condition(nameof(FakeRecord.Age), FilterOperator.Equals, 10),
            ]);

        Assert.Equal([30], Filter(and));
        Assert.Equal([10, 30], Filter(or));
    }

    [Fact]
    public void ApplyFilter_EmptyGroup_MatchesEverything()
    {
        Assert.Equal([10, 20, 30], Filter(new CompiledFilterGroup(LogicalOperator.And, [])));
    }

    [Theory]
    [InlineData(MatchMode.Exact, "BETA", new[] { 20 })]
    [InlineData(MatchMode.StartsWith, "al", new[] { 10 })]
    [InlineData(MatchMode.EndsWith, "HA", new[] { 10 })]
    [InlineData(MatchMode.Contains, "e", new[] { 20 })]
    public void ApplySearch_MatchModes_AreCaseInsensitive(MatchMode mode, string text, int[] expectedAges)
    {
        var search = new CompiledSearch(text, [new CompiledSearchField(Field(nameof(FakeRecord.Name)), mode, 1)]);

        Assert.Equal(expectedAges, Search(search));
    }

    [Fact]
    public void ApplySearch_SeveralFields_MatchAnyField()
    {
        var search = new CompiledSearch(
            "a",
            [
                new CompiledSearchField(Field(nameof(FakeRecord.Name)), MatchMode.StartsWith, 1),
                new CompiledSearchField(Field(nameof(FakeRecord.Name)), MatchMode.EndsWith, 2),
            ]);

        Assert.Equal([10, 20], Search(search));
    }

    [Fact]
    public void ApplySearch_WithoutFields_MatchesEverything()
    {
        Assert.Equal([10, 20, 30], Search(new CompiledSearch("x", [])));
    }

    [Fact]
    public void ApplySearch_UnknownMatchMode_ThrowsUnsupportedMatchMode()
    {
        var search = new CompiledSearch("x", [new CompiledSearchField(Field(nameof(FakeRecord.Name)), (MatchMode)99, 1)]);

        var exception = Assert.Throws<KaleidoValidationException>(() => Search(search));

        Assert.Equal(QueryableErrorCodes.UnsupportedMatchMode, exception.Code);
    }

    [Fact]
    public void ApplySort_OrdersBySequenceThenByFollowingSorts()
    {
        var records = new[]
        {
            new FakeRecord { Age = 1, Status = FakeStatus.Inactive, Count = 1 },
            new FakeRecord { Age = 2, Status = FakeStatus.Active, Count = 1 },
            new FakeRecord { Age = 3, Status = FakeStatus.Active, Count = 2 },
            new FakeRecord { Age = 4, Status = FakeStatus.Inactive, Count = 2 },
        }.AsQueryable();

        var result = CreateSut().ApplySort(
            records,
            [
                new CompiledSort(Field(nameof(FakeRecord.Count)), SortDirection.Descending, 1),
                new CompiledSort(Field(nameof(FakeRecord.Status)), SortDirection.Ascending, 0),
                new CompiledSort(Field(nameof(FakeRecord.Age)), SortDirection.Descending, 2),
            ]);

        Assert.Equal([3, 2, 4, 1], result.Select(x => x.Age));
    }

    [Fact]
    public void ApplySort_FirstSortDescendingThenAscending()
    {
        var records = new[]
        {
            new FakeRecord { Age = 1, Count = 1 },
            new FakeRecord { Age = 2, Count = 2 },
            new FakeRecord { Age = 3, Count = 2 },
        }.AsQueryable();

        var result = CreateSut().ApplySort(
            records,
            [
                new CompiledSort(Field(nameof(FakeRecord.Count)), SortDirection.Descending, 0),
                new CompiledSort(Field(nameof(FakeRecord.Age)), SortDirection.Ascending, 1),
            ]);

        Assert.Equal([2, 3, 1], result.Select(x => x.Age));
    }

    [Fact]
    public void ApplySort_UnknownDirection_ThrowsFrameworkException()
    {
        var exception = Assert.Throws<KaleidoFrameworkException>(
            () => CreateSut().ApplySort(
                Records.AsQueryable(),
                [new CompiledSort(Field(nameof(FakeRecord.Age)), (SortDirection)9, 0)]));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
    }

    [Fact]
    public void ApplySort_UnknownDirectionOnAFollowingSort_ThrowsFrameworkException()
    {
        var exception = Assert.Throws<KaleidoFrameworkException>(
            () => CreateSut().ApplySort(
                Records.AsQueryable(),
                [
                    new CompiledSort(Field(nameof(FakeRecord.Age)), SortDirection.Ascending, 0),
                    new CompiledSort(Field(nameof(FakeRecord.Count)), (SortDirection)9, 1),
                ]));

        Assert.Equal(FrameworkErrorCodes.TypeMismatch, exception.Code);
    }

    private static int[] Filter(CompiledFilterExpression filter) =>
        [.. CreateSut().ApplyFilter(Records.AsQueryable(), filter).Select(x => x.Age)];

    private static int[] Search(CompiledSearch search) =>
        [.. CreateSut().ApplySearch(Records.AsQueryable(), search).Select(x => x.Age)];

    private static CompiledFilterCondition Condition(string field, FilterOperator op, params object?[] values) =>
        new(Field(field), op, values);

    private static FieldMetadata Field(string name) =>
        new() { Name = name, FieldType = typeof(object), DataType = TestDataType };
}
