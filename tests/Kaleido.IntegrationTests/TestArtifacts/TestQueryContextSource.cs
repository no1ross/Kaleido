namespace Kaleido.IntegrationTests.TestArtifacts;

[QuerySource(
    Version = "1.0.0",
    DisplayName = "Test Query",
    Description = "Test query context")]
public sealed class TestQueryContextSource : IQuerySource<TestQueryContext>
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
