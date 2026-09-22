using Kaleido.Queryable.Exceptions;
using Kaleido.Queryable.Query;
using Xunit;

namespace Kaleido.Queryable.Abstractions.UnitTests;

public sealed class QueryRequestTests
{
    [Fact]
    public void QueryRequest_UsesEmptyQueryViewParametersByDefault()
    {
        IQueryRequest request = new QueryRequest();

        Assert.IsType<EmptyQueryViewParameters>(request.ViewParameters);
        Assert.Equal(typeof(EmptyQueryViewParameters), request.ViewParametersType);
    }

    [Fact]
    public void QueryRequestOfT_ExposesTypedAndUntypedParameters()
    {
        var parameters = new TestParameters { Category = "Alpha" };
        IQueryRequest request = new QueryRequest<TestParameters>(parameters, new QueryBody(SearchText: "alpha"));

        Assert.Same(parameters, ((QueryRequest<TestParameters>)request).ViewParameters);
        Assert.Same(parameters, request.ViewParameters);
        Assert.Equal(typeof(TestParameters), request.ViewParametersType);
        Assert.Equal("alpha", request.Query!.SearchText);
    }

    [Fact]
    public void QueryFilterNodeCreateCondition_CreatesConditionNode()
    {
        var node = QueryFilterNode.CreateCondition("Category", FilterOperator.Equals, "Alpha");

        Assert.NotNull(node.Condition);
        Assert.Null(node.Group);
        Assert.Equal("Category", node.Condition!.Field);
        Assert.Equal(FilterOperator.Equals, node.Condition.Operator);
        Assert.Equal("Alpha", node.Condition.Values.Single());
    }

    [Fact]
    public void QueryFilterNodeCreateGroup_CreatesGroupNode()
    {
        var child = QueryFilterNode.CreateCondition("Category", FilterOperator.Equals, "Alpha");
        var node = QueryFilterNode.CreateGroup(LogicalOperator.And, child);

        Assert.Null(node.Condition);
        Assert.NotNull(node.Group);
        Assert.Equal(LogicalOperator.And, node.Group!.Operator);
        Assert.Single(node.Group.Filters);
    }

    [Fact]
    public void QueryResult_PreservesConstructorValues()
    {
        var record = new TestRecord();
        var result = new QueryResult<TestRecord>(2, 3, 4, [record]);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(3, result.Offset);
        Assert.Equal(4, result.PageSize);
        Assert.Same(record, result.Results.Single());
    }

    [Fact]
    public void UnsupportedOperatorException_CreatesWithCorrectProperties()
    {
        var exception = new UnsupportedOperatorException("test-field", FilterOperator.Equals);

        Assert.Equal(QueryErrorCodes.UnsupportedOperator, exception.Code);
        Assert.Equal("test-field", exception.Field);
        Assert.Equal(FilterOperator.Equals, exception.Operator);
        Assert.Contains("test-field", exception.Message);
        Assert.Contains("Equals", exception.Message);
    }

    [Fact]
    public void UnsupportedMatchModeException_CreatesWithCorrectProperties()
    {
        var exception = new UnsupportedMatchModeException("test-field", MatchMode.Exact);

        Assert.Equal(QueryErrorCodes.UnsupportedMatchMode, exception.Code);
        Assert.Contains("test-field", exception.Message);
        Assert.Contains("Exact", exception.Message);
    }

    [Fact]
    public void PagingNotSupportedException_CreatesWithCorrectMessage()
    {
        var exception = new PagingNotSupportedException("test-record");

        Assert.Equal(QueryErrorCodes.PagingNotSupported, exception.Code);
        Assert.Contains("test-record", exception.Message);
    }

    [Fact]
    public void MissingParameterException_CreatesWithCorrectMessage()
    {
        var exception = new MissingParameterException("test-query", "test-param");

        Assert.Equal(QueryErrorCodes.MissingParameter, exception.Code);
        Assert.Contains("test-query", exception.Message);
        Assert.Contains("test-param", exception.Message);
    }

    [Fact]
    public void InvalidParameterTypeException_CreatesWithCorrectMessage()
    {
        var exception = new InvalidParameterTypeException("test-param", typeof(int), typeof(string));

        Assert.Equal(QueryErrorCodes.InvalidParameterType, exception.Code);
        Assert.Contains("test-param", exception.Message);
        Assert.Contains("Int32", exception.Message);
        Assert.Contains("String", exception.Message);
    }


    [Fact]
    public void InvalidSearchNodeException_CreatesWithCorrectMessage()
    {
        var exception = new InvalidSearchNodeException("test error message");

        Assert.Equal(QueryErrorCodes.InvalidSearchNode, exception.Code);
        Assert.Equal("test error message", exception.Message);
    }

    [Fact]
    public void EmptySearchGroupException_CreatesWithCorrectMessage()
    {
        var exception = new EmptySearchGroupException();

        Assert.Equal(QueryErrorCodes.EmptySearchGroup, exception.Code);
        Assert.Contains("at least one expression", exception.Message);
    }

    [Fact]
    public void MissingSearchTextException_CreatesWithCorrectMessage()
    {
        var exception = new MissingSearchTextException();

        Assert.Equal(QueryErrorCodes.MissingSearchText, exception.Code);
        Assert.Contains("required", exception.Message);
    }

    [Fact]
    public void UnsupportedRuntimeTypeException_CreatesWithCorrectProperties()
    {
        var exception = new UnsupportedRuntimeTypeException("test-name", typeof(string));

        Assert.Equal(QueryErrorCodes.UnsupportedRuntimeType, exception.Code);
        Assert.Equal("test-name", exception.Name);
        Assert.Equal(typeof(string), exception.ActualType);
        Assert.Contains("test-name", exception.Message);
        Assert.Contains("String", exception.Message);
    }

    [Fact]
    public void ValueConversionException_CreatesWithCorrectMessage()
    {
        var exception = new ValueConversionException("test-param", "test-value", typeof(int));

        Assert.Equal(QueryErrorCodes.InvalidParameterType, exception.Code);
        Assert.Contains("test-param", exception.Message);
        Assert.Contains("test-value", exception.Message);
        Assert.Contains("Int32", exception.Message);
    }

    [Fact]
    public void InvalidParameterValueException_CreatesWithCorrectMessage()
    {
        var exception = new InvalidParameterValueException("test-param", "test-value", typeof(int));

        Assert.Equal(QueryErrorCodes.InvalidParameterValue, exception.Code);
        Assert.Contains("test-param", exception.Message);
        Assert.Contains("test-value", exception.Message);
        Assert.Contains("Int32", exception.Message);
    }

    private sealed class TestParameters
    {
        public string Category { get; init; } = string.Empty;
    }

    private sealed class TestRecord
    {
    }
}
