using System.Reflection;
using Kaleido.Exceptions;

namespace Kaleido.Queryable.Metadata.UnitTests;

public sealed class QueryMetadataExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
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
