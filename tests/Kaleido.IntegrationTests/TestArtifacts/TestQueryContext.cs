namespace Kaleido.IntegrationTests.TestArtifacts;

public sealed record TestQueryContext : IQueryContext
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
