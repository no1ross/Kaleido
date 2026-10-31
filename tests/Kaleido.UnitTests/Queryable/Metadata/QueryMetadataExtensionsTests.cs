using System.Reflection;
using Kaleido.Exceptions;

namespace Kaleido.Queryable.Metadata.UnitTests;

public sealed class QueryMetadataExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly QuerySourceAttribute SourceAttribute = new() { DisplayName = "Paged", Description = "Paged.", DefaultSortField = nameof(PagedContext.Id) };
    private static readonly QueryViewAttribute ViewAttribute = new() { DisplayName = "Paged", Description = "Paged.", DefaultSortField = nameof(PagedContext.Id) };

    // #519: both [Pageable] limits are required and consistent. A bare [Pageable] (0 and 0) returned
    // empty pages with a non-zero TotalCount; a default above the maximum was silently clamped.
    [Theory]
    [InlineData(typeof(BarePaged), 0, 0)]
    [InlineData(typeof(ZeroDefaultPaged), 0, 10)]
    [InlineData(typeof(ZeroMaxPaged), 10, 0)]
    [InlineData(typeof(NegativeDefaultPaged), -1, 10)]
    [InlineData(typeof(DefaultAboveMaxPaged), 20, 10)]
    public void ToSourceAndViewPageable_LimitsThatAreNotPositiveAndOrdered_FailStartup_NamingTheCapabilityAndBothLimits(Type capability, int defaultSize, int maxSize)
    {
        var source = Assert.Throws<KaleidoConfigurationException>(() => capability.ToSourcePageable(typeof(PagedContext), SourceAttribute, QuerySourceKind.Local));
        var view = Assert.Throws<KaleidoConfigurationException>(() => capability.ToViewPageable(typeof(PagedContext), ViewAttribute));

        foreach (var (exception, kind) in new[] { (source, "Query source"), (view, "Query view") })
        {
            Assert.Equal(QueryableErrorCodes.InvalidRegistration, exception.Code);
            Assert.StartsWith($"{kind} '{capability.Name}' declares [Pageable(DefaultSize = {defaultSize}, MaxSize = {maxSize})]", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ToSourcePageable_ValidLimits_AreKept_AndAnEqualDefaultAndMaximumIsValid()
    {
        var pageable = typeof(EqualLimitsPaged).ToSourcePageable(typeof(PagedContext), SourceAttribute, QuerySourceKind.Local);

        Assert.Equal(new PageableMetadata(10, 10, nameof(PagedContext.Id)), pageable);
        Assert.Null(typeof(PagedContext).ToSourcePageable(typeof(PagedContext), SourceAttribute, QuerySourceKind.Local));
    }

    private sealed class PagedContext
    {
        [Sortable]
        public int Id { get; init; }
    }

    [Pageable]
    private sealed class BarePaged;

    [Pageable(DefaultSize = 0, MaxSize = 10)]
    private sealed class ZeroDefaultPaged;

    [Pageable(DefaultSize = 10, MaxSize = 0)]
    private sealed class ZeroMaxPaged;

    [Pageable(DefaultSize = -1, MaxSize = 10)]
    private sealed class NegativeDefaultPaged;

    [Pageable(DefaultSize = 20, MaxSize = 10)]
    private sealed class DefaultAboveMaxPaged;

    [Pageable(DefaultSize = 10, MaxSize = 10)]
    private sealed class EqualLimitsPaged;

    [Theory]
    [InlineData(nameof(OperatorFitContext.RangeOnString), nameof(FilterOperator.GreaterThan))]
    [InlineData(nameof(OperatorFitContext.RangeOnEnum), nameof(FilterOperator.Between))]
    [InlineData(nameof(OperatorFitContext.RangeOnBool), nameof(FilterOperator.LessThan))]
    [InlineData(nameof(OperatorFitContext.ContainsOnInt), nameof(FilterOperator.Contains))]
    [InlineData(nameof(OperatorFitContext.StartsWithOnDate), nameof(FilterOperator.StartsWith))]
    [InlineData(nameof(OperatorFitContext.IsNullOnInt), nameof(FilterOperator.IsNull))]
    [InlineData(nameof(OperatorFitContext.IsTrueOnString), nameof(FilterOperator.IsTrue))]
    [InlineData(nameof(OperatorFitContext.EqualsOnStructWithoutEquality), nameof(FilterOperator.Equals))]
    public void ToFieldMetadata_AnOperatorThatDoesNotFitTheFieldsType_FailsStartup(string property, string @operator)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Field(property));

        Assert.Equal(QueryableErrorCodes.InvalidRegistration, exception.Code);
        Assert.Contains($"'{nameof(OperatorFitContext)}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{property}'", exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{@operator}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(nameof(OperatorFitContext.Text))]
    [InlineData(nameof(OperatorFitContext.Amount))]
    [InlineData(nameof(OperatorFitContext.When))]
    [InlineData(nameof(OperatorFitContext.Day))]
    [InlineData(nameof(OperatorFitContext.Status))]
    [InlineData(nameof(OperatorFitContext.Flag))]
    [InlineData(nameof(OperatorFitContext.Owner))]
    public void ToFieldMetadata_OperatorsThatFitTheFieldsType_AreKept(string property)
    {
        var field = Field(property);

        Assert.Equal(
            typeof(OperatorFitContext).GetProperty(property)!.GetCustomAttribute<FilterableAttribute>()!.Operators,
            field.FilterOperators);
    }

    private static FieldMetadata Field(string property)
    {
        var typeDescriber = new Mock<ITypeDescriber>();
        typeDescriber
            .Setup(x => x.GetDescriptor(It.IsAny<PropertyInfo>()))
            .Returns(new DataTypeDescriptor("mock-type"));

        return typeof(OperatorFitContext).GetProperty(property)!.ToFieldMetadata(typeDescriber.Object);
    }

    private enum FitStatus
    {
        Open,
        Closed,
    }

    private readonly struct NoEquality;

    private sealed record Person;

    private sealed record OperatorFitContext : IQueryContext
    {
        [Filterable(FilterOperator.GreaterThan)]
        public string? RangeOnString { get; init; }

        [Filterable(FilterOperator.Between)]
        public FitStatus RangeOnEnum { get; init; }

        [Filterable(FilterOperator.LessThan)]
        public bool RangeOnBool { get; init; }

        [Filterable(FilterOperator.Contains)]
        public int ContainsOnInt { get; init; }

        [Filterable(FilterOperator.StartsWith)]
        public DateTime StartsWithOnDate { get; init; }

        [Filterable(FilterOperator.IsNull)]
        public int IsNullOnInt { get; init; }

        [Filterable(FilterOperator.IsTrue)]
        public string? IsTrueOnString { get; init; }

        [Filterable(FilterOperator.Equals)]
        public NoEquality EqualsOnStructWithoutEquality { get; init; }

        [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.In, FilterOperator.NotIn, FilterOperator.IsNull, FilterOperator.IsNotNull)]
        public string? Text { get; init; }

        [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.NotBetween, FilterOperator.In, FilterOperator.NotIn)]
        public decimal Amount { get; init; }

        [Filterable(FilterOperator.GreaterThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.IsNull, FilterOperator.IsNotNull)]
        public DateTime? When { get; init; }

        [Filterable(FilterOperator.Equals, FilterOperator.GreaterThanOrEqual, FilterOperator.Between)]
        public DateOnly Day { get; init; }

        [Filterable(FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn)]
        public FitStatus Status { get; init; }

        [Filterable(FilterOperator.IsTrue, FilterOperator.IsFalse, FilterOperator.Equals, FilterOperator.IsNull)]
        public bool? Flag { get; init; }

        [Filterable(FilterOperator.Equals, FilterOperator.IsNull, FilterOperator.IsNotNull)]
        public Person? Owner { get; init; }
    }
}
