using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Queryable.FilterOperatorTypeAnalyzer>;

namespace Kaleido.Analyzers.Queryable.UnitTests;

public sealed class FilterOperatorTypeAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Queryable.FilterOperatorTypeAnalyzer>
{
    protected override global::Kaleido.Analyzers.Queryable.FilterOperatorTypeAnalyzer CreateSut() =>
        new();

    private const string FilterableStub = @"
namespace Kaleido.Queryable
{
    public enum FilterOperator
    {
        Equals, NotEquals, GreaterThan, GreaterThanOrEqual, LessThan, LessThanOrEqual,
        Contains, NotContains, StartsWith, EndsWith, In, NotIn, Between, NotBetween,
        IsNull, IsNotNull, IsTrue, IsFalse
    }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public sealed class FilterableAttribute : System.Attribute
    {
        public FilterableAttribute(params FilterOperator[] operators) { }
    }
}

public enum Status { Open, Closed }

public readonly struct NoEquality { }

public sealed class Person { }";

    private const string Compare = "the type cannot be compared (a number, date, time or other type with comparison operators)";
    private const string Text = "it applies to string fields only";
    private const string Bool = "it applies to bool fields only";
    private const string NotNullable = "the type cannot be null";
    private const string NoEqualityOperator = "the type has no equality operator";

    private static readonly DiagnosticResult Expected =
        new("KAL2023", DiagnosticSeverity.Error);

    [Theory]
    [InlineData("string", "GreaterThan", Compare)]
    [InlineData("string?", "Between", Compare)]
    [InlineData("Status", "LessThanOrEqual", Compare)]
    [InlineData("bool", "GreaterThanOrEqual", Compare)]
    [InlineData("char", "LessThan", Compare)]
    [InlineData("int", "Contains", Text)]
    [InlineData("System.DateTime", "StartsWith", Text)]
    [InlineData("System.Guid", "EndsWith", Text)]
    [InlineData("string", "IsTrue", Bool)]
    [InlineData("int?", "IsFalse", Bool)]
    [InlineData("int", "IsNull", NotNullable)]
    [InlineData("System.DateOnly", "IsNotNull", NotNullable)]
    [InlineData("NoEquality", "Equals", NoEqualityOperator)]
    [InlineData("NoEquality?", "NotEquals", NoEqualityOperator)]
    public async Task AnOperatorThatDoesNotFitThePropertysType_Reports(string type, string @operator, string reason)
    {
        await RunAsync(@"
#nullable enable
using Kaleido.Queryable;
public sealed class MemberContext
{
    [{|#0:Filterable(FilterOperator." + @operator + @")|}]
    public " + type + @" Field { get; init; } = default!;
}
" + FilterableStub,
            Expected.WithLocation(0).WithArguments(@operator, "Field", type, reason));
    }

    [Fact]
    public async Task EachMisfitOperator_ReportsOnce()
    {
        await RunAsync(@"
using Kaleido.Queryable;
public sealed class MemberContext
{
    [{|#0:Filterable(FilterOperator.Equals, FilterOperator.GreaterThan, FilterOperator.IsTrue)|}]
    public string Code { get; init; } = """";
}
" + FilterableStub,
            Expected.WithLocation(0).WithArguments("GreaterThan", "Code", "string", Compare),
            Expected.WithLocation(0).WithArguments("IsTrue", "Code", "string", Bool));
    }

    [Theory]
    [InlineData("string?", "Equals, FilterOperator.NotEquals, FilterOperator.Contains, FilterOperator.NotContains, FilterOperator.StartsWith, FilterOperator.EndsWith, FilterOperator.In, FilterOperator.NotIn, FilterOperator.IsNull, FilterOperator.IsNotNull")]
    [InlineData("decimal", "Equals, FilterOperator.GreaterThan, FilterOperator.GreaterThanOrEqual, FilterOperator.LessThan, FilterOperator.LessThanOrEqual, FilterOperator.Between, FilterOperator.NotBetween, FilterOperator.In")]
    [InlineData("int?", "GreaterThan, FilterOperator.Between, FilterOperator.IsNull, FilterOperator.IsNotNull")]
    [InlineData("System.DateTime?", "LessThan, FilterOperator.IsNull")]
    [InlineData("System.DateTimeOffset", "Equals, FilterOperator.GreaterThanOrEqual")]
    [InlineData("System.DateOnly", "Between")]
    [InlineData("System.TimeSpan", "LessThanOrEqual")]
    [InlineData("Status", "Equals, FilterOperator.NotEquals, FilterOperator.In, FilterOperator.NotIn")]
    [InlineData("bool?", "IsTrue, FilterOperator.IsFalse, FilterOperator.Equals, FilterOperator.IsNull")]
    [InlineData("System.Guid", "Equals, FilterOperator.In")]
    [InlineData("Person?", "Equals, FilterOperator.IsNull, FilterOperator.IsNotNull")]
    public async Task OperatorsThatFitThePropertysType_NoDiagnostic(string type, string operators)
    {
        await RunAsync(@"
#nullable enable
using Kaleido.Queryable;
public sealed class MemberContext
{
    [Filterable(FilterOperator." + operators + @")]
    public " + type + @" Field { get; init; } = default!;
}
" + FilterableStub);
    }
}
