using Kaleido.Observability;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Observability;

namespace Kaleido.Queryable.UnitTests.Eventing;

public sealed class QueryEventFactoryTests
{
    private readonly KaleidoServiceOptions _options = new() { ServiceName = "test-service" };
    private readonly QueryEventFactory _factory;

    public QueryEventFactoryTests()
    {
        _factory = new QueryEventFactory(_options);
    }

    [Fact]
    public void CreateQueryExecuted_WhenCorrelationIsNull_Throws()
    {
        var details = new QueryObservationDetails("test-context", "test-view", false, QueryExecutionMode.DelegatedContext);
        var request = new QueryRequest();
        var result = new QueryResult<TestView>(0, 0, 10, []);

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateQueryExecuted(null!, details, request, result));
    }

    [Fact]
    public void CreateQueryExecuted_WhenDetailsIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var request = new QueryRequest();
        var result = new QueryResult<TestView>(0, 0, 10, []);

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateQueryExecuted(correlation, null!, request, result));
    }

    [Fact]
    public void CreateQueryExecuted_WhenRequestIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var details = new QueryObservationDetails("test-context", "test-view", false, QueryExecutionMode.DelegatedContext);
        var result = new QueryResult<TestView>(0, 0, 10, []);

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateQueryExecuted(correlation, details, null!, result));
    }

    [Fact]
    public void CreateQueryExecuted_WhenResultIsNull_Throws()
    {
        var correlation = new KaleidoCorrelationContext();
        var details = new QueryObservationDetails("test-context", "test-view", false, QueryExecutionMode.DelegatedContext);
        var request = new QueryRequest();

        Assert.Throws<ArgumentNullException>(() =>
            _factory.CreateQueryExecuted<TestView>(correlation, details, request, null!));
    }

    [Fact]
    public void CreateQueryExecuted_CreatesEventWithCorrectContext()
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request" };
        var details = new QueryObservationDetails("test-context", "test-view", false, QueryExecutionMode.DelegatedContext);
        var request = new QueryRequest();
        var result = new QueryResult<TestView>(0, 0, 10, []);

        var envelope = _factory.CreateQueryExecuted<TestView>(correlation, details, request, result);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Context);
        Assert.Equal("test-request", envelope.Context.RequestId);
        Assert.Equal("test-service", envelope.Context.ServiceName);
        Assert.Equal("test-context", envelope.Context.QueryContextName);
        Assert.Equal("test-view", envelope.Context.QueryViewName);
    }

    [Fact]
    public void CreateQueryExecuted_CreatesEventWithCorrectData()
    {
        var correlation = new KaleidoCorrelationContext { RequestId = "test-request" };
        var details = new QueryObservationDetails("test-context", "test-view", false, QueryExecutionMode.DelegatedContext);
        var request = new QueryRequest();
        var results = new[] { new TestView { Id = 1, Name = "test" } };
        var result = new QueryResult<TestView>(1, 0, 10, results);

        var envelope = _factory.CreateQueryExecuted<TestView>(correlation, details, request, result);

        Assert.NotNull(envelope);
        Assert.NotNull(envelope.Event);
        Assert.Equal("test-context", envelope.Event.QueryContextName);
        Assert.Equal("test-view", envelope.Event.QueryViewName);
        Assert.False(envelope.Event.IsDirectQuery);
        Assert.Equal(1, envelope.Event.TotalCount);
        Assert.Equal(1, envelope.Event.ReturnedCount);
        Assert.Equal(10, envelope.Event.PageSize);
        Assert.Equal(0, envelope.Event.Offset);
    }

    [Fact]
    public void CreateQueryExecuted_WhenDirectQuery_SetsIsDirectQueryTrue()
    {
        var correlation = new KaleidoCorrelationContext();
        var details = new QueryObservationDetails("test-context", "test-view", true, QueryExecutionMode.DirectContext);
        var request = new QueryRequest();
        var result = new QueryResult<TestView>(0, 0, 10, []);

        var envelope = _factory.CreateQueryExecuted<TestView>(correlation, details, request, result);

        Assert.True(envelope.Event.IsDirectQuery);
    }

    [Fact]
    public void CreateQueryExecuted_WhenNoSearchText_SetsSearchTextNull()
    {
        var correlation = new KaleidoCorrelationContext();
        var details = new QueryObservationDetails("test-context", "test-view", false, QueryExecutionMode.DelegatedContext);
        var request = new QueryRequest { Query = new QueryBody() };
        var result = new QueryResult<TestView>(0, 0, 10, []);

        var envelope = _factory.CreateQueryExecuted<TestView>(correlation, details, request, result);

        Assert.Null(envelope.Event.SearchText);
    }

    [Fact]
    public void CreateQueryExecuted_WhenNoSort_SetsSortCountZero()
    {
        var correlation = new KaleidoCorrelationContext();
        var details = new QueryObservationDetails("test-context", "test-view", false, QueryExecutionMode.DelegatedContext);
        var request = new QueryRequest { Query = new QueryBody() };
        var result = new QueryResult<TestView>(0, 0, 10, []);

        var envelope = _factory.CreateQueryExecuted<TestView>(correlation, details, request, result);

        Assert.Equal(0, envelope.Event.SortCount);
    }

    [Fact]
    public void CreateQueryExecuted_WhenNoFilter_SetsFilterProvidedFalse()
    {
        var correlation = new KaleidoCorrelationContext();
        var details = new QueryObservationDetails("test-context", "test-view", false, QueryExecutionMode.DelegatedContext);
        var request = new QueryRequest { Query = new QueryBody() };
        var result = new QueryResult<TestView>(0, 0, 10, []);

        var envelope = _factory.CreateQueryExecuted<TestView>(correlation, details, request, result);

        Assert.False(envelope.Event.FilterProvided);
    }

    private sealed class TestView
    {
        public int Id { get; init; }
        public string Name { get; init; } = string.Empty;
    }
}
