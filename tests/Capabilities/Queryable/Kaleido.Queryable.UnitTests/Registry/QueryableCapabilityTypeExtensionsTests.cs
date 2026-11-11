using Kaleido.UnitTests;

namespace Kaleido.Queryable.Registry.UnitTests;

public sealed class QueryableCapabilityTypeExtensionsTests : SutFixture
{
    [Fact]
    public void LocalSources_ExposeOnlyTheirDeclaredSyncOrAsyncShape()
    {
        Assert.Equal(typeof(IQuerySource<Context>), Assert.Single(typeof(Source).GetSyncSourceInterfaces()));
        Assert.Empty(typeof(Source).GetAsyncSourceInterfaces());
        Assert.True(typeof(Source).IsLocalQuerySource());
        Assert.False(typeof(Source).IsQueryView());

        Assert.Equal(typeof(IQuerySourceAsync<Context>), Assert.Single(typeof(AsyncSource).GetAsyncSourceInterfaces()));
        Assert.Empty(typeof(AsyncSource).GetSyncSourceInterfaces());
        Assert.True(typeof(AsyncSource).IsLocalQuerySource());
    }

    [Fact]
    public void DelegatedSource_DoesNotBecomeALocalSourceOrView()
    {
        Assert.Equal(typeof(IDelegatedQuerySource<Context, Result, Parameters>), Assert.Single(typeof(DelegatedSource).GetDelegatedSourceInterfaces()));
        Assert.True(typeof(DelegatedSource).IsDelegatedQuerySource());
        Assert.False(typeof(DelegatedSource).IsLocalQuerySource());
        Assert.False(typeof(DelegatedSource).IsQueryView());
    }

    [Fact]
    public void Views_ExposeTheirSourceAndExecutionShape()
    {
        Assert.Equal(typeof(IQueryViewSource<Source, Context, Result, EmptyQueryViewParameters>), Assert.Single(typeof(View).GetSyncViewInterfaces()));
        Assert.Equal(typeof(IQueryViewSourceAsync<Source, Context, Result, EmptyQueryViewParameters>), Assert.Single(typeof(AsyncView).GetAsyncViewInterfaces()));
        Assert.True(typeof(View).IsQueryView());
        Assert.True(typeof(AsyncView).IsQueryView());
        Assert.False(typeof(View).IsLocalQuerySource());
    }

    private sealed record Context(int Id) : IQueryContext;

    private sealed record Result(string Name);

    private sealed record Parameters(Guid Id) : IQueryParameters;

    private sealed class Source : IQuerySource<Context>
    {
        public IQueryable<Context> CreateQuery(QueryExecutionContext executionContext) => Array.Empty<Context>().AsQueryable();
    }

    private sealed class AsyncSource : IQuerySourceAsync<Context>
    {
        public Task<IQueryable<Context>> CreateQueryAsync(QueryExecutionContext executionContext, CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<Context>().AsQueryable());
    }

    private sealed class DelegatedSource : IDelegatedQuerySource<Context, Result, Parameters>
    {
        public Task<QueryResult<Result>> ExecuteAsync(IQueryRequest<Parameters> request, CancellationToken cancellationToken) =>
            Task.FromResult(new QueryResult<Result>(0, 0, 0, []));
    }

    private sealed class View : IQueryViewSource<Source, Context, Result>
    {
        public IQueryable<Result> CreateView(IQueryable<Context> query, QueryExecutionContext executionContext) => Array.Empty<Result>().AsQueryable();
    }

    private sealed class AsyncView : IQueryViewSourceAsync<Source, Context, Result>
    {
        public Task<IQueryable<Result>> CreateViewAsync(IQueryable<Context> query, QueryExecutionContext executionContext, CancellationToken cancellationToken) =>
            Task.FromResult(Array.Empty<Result>().AsQueryable());
    }
}
