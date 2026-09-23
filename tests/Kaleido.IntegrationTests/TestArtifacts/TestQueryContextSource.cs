namespace Kaleido.IntegrationTests.TestArtifacts;

public sealed class TestQueryContextSource : IQueryContextSource<TestQueryContext>
{
    private readonly TestDbContext _dbContext;

    public TestQueryContextSource(TestDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IQueryable<TestQueryContext> CreateQuery(QueryExecutionContext executionContext)
    {
        return _dbContext.TestEntities
            .Select(e => new TestQueryContext { Id = e.Id, Name = e.Name })
            .AsQueryable();
    }
}
